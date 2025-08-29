using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Filters;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Controllers
{
    public class ReportController : Controller
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly ICodeFileManager _codeFileManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IConfiguration _configuration;
        private readonly CosmosClient _cosmosClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IBlobCodeFileRepository _codeFileRepository;


        public ReportController(
            IAuthorizationService authorizationService,
            ICodeFileManager codeFileManager,
            IAPIRevisionsManager apiRevisionsManager,
            IBlobCodeFileRepository codeFileRepository,
            IConfiguration configuration)
        {
            _authorizationService = authorizationService;
            _codeFileManager = codeFileManager;
            _apiRevisionsManager = apiRevisionsManager;
            _configuration = configuration;
            _codeFileRepository = codeFileRepository;

            // Initialize Azure clients
            var credential = new DefaultAzureCredential();
            var cosmosEndpoint = _configuration["CosmosEndpoint"];
            _cosmosClient = new CosmosClient(cosmosEndpoint, credential);

            var storageAccountUrl = _configuration["StorageAccountUrl"];
            _blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl), credential);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> GenerateReport(
            string? language = null,
            DateTime? startDate = null,
            int maxResults = 300)
        {
            try
            {
                startDate = startDate ?? DateTime.UtcNow.AddDays(-180);
                // Query CosmosDB for API revisions
                var queryResults = await QueryCosmosDBAsync(language, startDate, maxResults);
                if (!queryResults.Any())
                {
                    return Ok(new { message = "No documents found in CosmosDB query.", data = new List<object>() });
                }

                var languages = queryResults.Select(r => r.Language).Distinct().ToList();

                var reportResult = new ReportResult();
                var duplicateLanguageReport = new DuplicateReportResult();
                var apiViewDocuments = new List<ApiViewDocument>();

                // Process each document and find the latest file
                foreach (var document in queryResults)
                {
                    try
                    {

                        var codeFile = await _codeFileRepository.GetCodeFileAsync(document, false);
                        if (codeFile != null)
                        {
                            if (AreLineIdsDuplicate(codeFile.CodeFile, out var duplicateLineIds))
                            {
                                // Handle duplicate line IDs
                                duplicateLanguageReport.Records.Add(new DuplicateRecord()
                                {
                                    Language = document.Language,
                                    PackageName = document.PackageName,
                                    ReviewId = document.ReviewId,
                                    RevisionId = document.Id,
                                    DuplicateLineIds = duplicateLineIds,
                                });
                            }
                       
                        }
                    }
                    catch (Exception e)
                    {

                        Console.WriteLine($"FAILED {document.Language}");


                    }

                }


                return Ok(new
                {
                    message = $"Report generated with {reportResult.Rows.Count} rows.",
                    language = duplicateLanguageReport.Records.Select(r => r.Language).Distinct(),
                    duplicateLanguages = duplicateLanguageReport
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { 
                    error = ex.Message,
                    stackTrace = ex.StackTrace 
                });
            }
        }

        [TypeFilter(typeof(ApiKeyAuthorizeAsyncFilter))]
        [HttpGet]
        public async Task<ActionResult> GenerateCsvReport(
            string? language = null,
            DateTime? startDate = null,
            int maxResults = 100)
        {
            try
            {
                // Get the report data
                var reportAction = await GenerateReport(language, startDate, maxResults);
                if (reportAction is not OkObjectResult okResult)
                {
                    return reportAction;
                }

                var reportData = okResult.Value as dynamic;
                var rows = reportData.data as List<ReportRow>;

                if (rows == null || !rows.Any())
                {
                    return BadRequest("No data available for CSV generation.");
                }

                // Generate CSV content
                var csv = new StringBuilder();
                csv.AppendLine("CreatedOn,ParserVersion,Language,APIRevisionId,PackageName,PackageVersion,HandwrittenLinesCount,TotalLines,HandwrittenPercentage,DuplicateLineIds");

                foreach (var row in rows)
                {
                    var handwrittenPercentage = row.TotalLines > 0 ? 
                        Math.Round((double)row.HandwrittenLines / row.TotalLines * 100, 2) : 0;

                    csv.AppendLine($"{EscapeCsv(row.CreatedOn.ToString("o"))}," +
                                 $"{EscapeCsv(row.ParserVersion)}," +
                                 $"{EscapeCsv(row.Language)}," +
                                 $"{EscapeCsv(row.RevisionId)}," +
                                 $"{EscapeCsv(row.PackageName)}," +
                                 $"{EscapeCsv(row.PackageVersion)}," +
                                 $"{row.HandwrittenLines}," +
                                 $"{row.TotalLines}," +
                                 $"{handwrittenPercentage}," +
                                 $"{EscapeCsv(row.DuplicateLineIds)}");
                }

                var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"APIView_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(csvBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { 
                    error = ex.Message 
                });
            }
        }


        private async Task<List<APIRevisionListItemModel>> QueryCosmosDBAsync(string? language = null, DateTime? startDate = null, int maxResults = 100)
        {
            var database = _cosmosClient.GetDatabase("APIViewV2");
            var container = database.GetContainer("APIRevisions");

            var whereConditions = new List<string>();
            var parameters = new List<(string name, object value)>();

            if (startDate.HasValue)
            {
                whereConditions.Add("c.CreatedOn > @startDate");
                parameters.Add(("@startDate", startDate.Value.ToString("o")));
            }

            var whereClause = whereConditions.Any() ? $"WHERE {string.Join(" AND ", whereConditions)}" : "";

            var query = $@"
                SELECT c.id as Id, c.Files, c.LastUpdatedOn, c.PackageName, c.Language, c.ReviewId
                FROM c
                {whereClause}
                ORDER BY c.LastUpdatedOn DESC
                OFFSET 0 LIMIT {maxResults}";

            var queryDefinition = new QueryDefinition(query);
            foreach (var (name, value) in parameters)
            {
                queryDefinition.WithParameter(name, value);
            }

            var revisions = new List<APIRevisionListItemModel>();
            using FeedIterator<APIRevisionListItemModel> feedIterator = container.GetItemQueryIterator<APIRevisionListItemModel>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<APIRevisionListItemModel> response = await feedIterator.ReadNextAsync();
                revisions.AddRange(response);
            }

            // Filter to get 15 distinct package names per language (latest revision per package)
            var filteredResults = revisions
                .Where(r => !string.IsNullOrEmpty(r.PackageName))
                .GroupBy(r => new { r.Language, r.PackageName })
                .Select(g => g.OrderByDescending(r => r.LastUpdatedOn).First()) // Get latest revision per package
                .GroupBy(r => r.Language)
                .SelectMany(languageGroup => languageGroup
                    .OrderByDescending(r => r.LastUpdatedOn)
                    .Take(15)) // Take 15 distinct packages per language
                .ToList();

            return filteredResults;
        }

        private async Task<ApiViewDocument?> DownloadFileFromBlobAsync(string documentId, string fileId)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("codefiles");
                var blobPath = $"{documentId}/{fileId}";
                var blobClient = containerClient.GetBlobClient(blobPath);

                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    return null;
                }

                var downloadResult = await blobClient.DownloadContentAsync();
                var jsonContent = downloadResult.Value.Content.ToString();

                var apiViewDoc = JsonSerializer.Deserialize<ApiViewDocument>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return apiViewDoc;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<MemoryStream?> DownloadFileFromBlobAsyncRaw(string documentId, string fileId)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("codefiles");
                var blobPath = $"{documentId}/{fileId}";
                var blobClient = containerClient.GetBlobClient(blobPath);

                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    return null;
                }

                var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);
                
                // Reset position to the beginning for reading
                memoryStream.Position = 0;
                
                return memoryStream;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private LinesAnalysis AnalyzeApiViewDocument(ApiViewDocument apiViewDoc)
        {
            if (apiViewDoc?.ReviewLines == null)
            {
                return new LinesAnalysis { TotalLines = 0, HandwrittenLines = 0 };
            }

            var linesWithContent = apiViewDoc.ReviewLines
                .Where(line => !string.IsNullOrEmpty(line.LineId) && line.Tokens.Any())
                .ToList();

            // Count handwritten lines (lines that are not auto-generated)
            var handwrittenLines = linesWithContent
                .Count(line => !IsAutoGeneratedLine(line));

            return new LinesAnalysis 
            { 
                TotalLines = linesWithContent.Count, 
                HandwrittenLines = handwrittenLines,
                DuplicateLineIds = GetDuplicateLineIds(apiViewDoc)
            };
        }

        private bool IsAutoGeneratedLine(ReviewLine line)
        {
            // This is a simplified logic - you might want to enhance this based on your specific criteria
            var lineContent = string.Join("", line.Tokens.Select(t => t.Value));
            
            // Examples of auto-generated patterns (adjust based on your needs)
            return lineContent.Contains("__pycache__") ||
                   lineContent.Contains("# Generated") ||
                   lineContent.StartsWith("# Auto-generated") ||
                   string.IsNullOrWhiteSpace(lineContent);
        }

        private string GetDuplicateLineIds(ApiViewDocument apiViewDoc)
        {
            var duplicateLineIds = apiViewDoc.ReviewLines
                .Where(line => !string.IsNullOrEmpty(line.LineId))
                .GroupBy(line => line.LineId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            return string.Join(", ", duplicateLineIds);
        }

        private bool AreLineIdsDuplicate(CodeFile codeFile, out string duplicateLineId)
        {
            var lines = codeFile.GetApiLines(skipDocs: true);
            var duplicateLineIds = lines
                .Where(line => !string.IsNullOrEmpty(line.lineId))
                .GroupBy(line => line.lineId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            duplicateLineId = string.Join(", ", duplicateLineIds);
            return duplicateLineIds.Count > 0;
        }

        private static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            if (input.Contains(',') || input.Contains('"') || input.Contains('\n') || input.Contains('\r'))
            {
                return $"\"{input.Replace("\"", "\"\"")}\"";
            }

            return input;
        }
    }

    // Supporting classes for the report functionality
    public class ReportResult
    {
        public List<ReportRow> Rows { get; set; } = new();
    }

    public class DuplicateReportResult
    {
        public List<DuplicateRecord> Records { get; set; } = new();
    }

    public class DuplicateRecord
    {
        public string Language { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string ReviewId { get; set; } = string.Empty;
        public string RevisionId { get; set; } = string.Empty;
        public string DuplicateLineIds { get; set; } = string.Empty;
    }

    public class ReportRow
    {
        public string RevisionId { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string PackageVersion { get; set; } = string.Empty;
        public string ParserVersion { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public int TotalLines { get; set; }
        public int HandwrittenLines { get; set; }
        public DateTime CreatedOn { get; set; }
        public string DuplicateLineIds { get; set; } = string.Empty;
    }

    public class LinesAnalysis
    {
        public int TotalLines { get; set; }
        public int HandwrittenLines { get; set; }
        public string DuplicateLineIds { get; set; } = string.Empty;
    }

    public class CosmosQueryResult
    {
        public string Id { get; set; } = string.Empty;
        public DateTime LastUpdatedOn { get; set; }
        public List<FileInfo> Files { get; set; } = new();
        public string PackageName { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
    }

    public class FileInfo
    {
        public string FileId { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        public string? FileName { get; set; }
        public string? VersionString { get; set; }
    }

    public class ApiViewDocument
    {
        public List<ReviewLine> ReviewLines { get; set; } = new();
        public string? CrossLanguagePackageId { get; set; }
        public string Language { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string PackageVersion { get; set; } = string.Empty;
        public string ParserVersion { get; set; } = string.Empty;
    }

    public class ReviewLine
    {
        public string LineId { get; set; } = string.Empty;
        public string? CrossLanguageId { get; set; }
        public List<Token> Tokens { get; set; } = new();
        public List<ReviewLine> Children { get; set; } = new();
        public string? RelatedToLine { get; set; }
    }

    public class Token
    {
        public int Kind { get; set; }
        public string Value { get; set; } = string.Empty;
        public bool? SkipDiff { get; set; }
        public bool HasSuffixSpace { get; set; }
        public bool HasPrefixSpace { get; set; }
        public List<string> RenderClasses { get; set; } = new();
    }
}
