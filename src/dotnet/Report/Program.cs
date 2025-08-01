using System.Text;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Report.Models;
using System.Text.Json;
using Report.Helper;

namespace Report
{
    public class Program
    {
        private static IConfiguration? _configuration;
        private static CosmosClient? _cosmosClient;
        private static BlobServiceClient? _blobServiceClient;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("APIView File Downloader Starting...");

            try
            {
                // Initialize configuration
                InitializeConfiguration();

                // Initialize Azure clients
                InitializeAzureClients();

                // Query CosmosDB
                var queryResults = await QueryCosmosDBAsync();
                if (!queryResults.Any())
                {
                    Console.WriteLine("No documents found in CosmosDB query.");
                    return;
                }

                Console.WriteLine($"Found {queryResults.Count} documents from CosmosDB.");


                Models.Report reportResult = new Models.Report();
                List<ApiViewDocument> apiViewDocuments = new List<ApiViewDocument>();
                // Process each document and find the latest file
                foreach (var document in queryResults)
                {
                    Console.WriteLine($"Processing document ID: {document.Id}");
                    
                    if (document.Files?.Any() == true)
                    {
                        // Get the latest file by CreationDate
                        var latestFile = document.Files
                            .OrderByDescending(f => f.CreationDate)
                            .First();

                        Console.WriteLine($"Latest file ID: {latestFile.FileId}, Language: {latestFile.Language}, Created: {latestFile.CreationDate}");

                        // Download the file from blob storage
                        var result = await DownloadFileFromBlobAsync(document.Id, latestFile.FileId);
                        if (result != null)
                        {
                            apiViewDocuments.Add(result);
                            var linesAnalysis = IdentifyHandwrittenLines.GetLinesAnalysis(result);
                            reportResult.Row.Add(new RowReport()
                            {
                                PackageName = result.PackageName,
                                RevisionId = document.Id,
                                TotalLines = linesAnalysis.TotalLines,
                                HandwrittenLines = linesAnalysis.HandwrittenLines,
                                Language = result.Language,
                                PackageVersion = result.PackageVersion,
                                ParserVersion = result.ParserVersion,
                                CreatedOn = latestFile.CreationDate,

                            });
                            Console.WriteLine($"File {latestFile.FileId} downloaded and parsed successfully.");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to download or parse file {latestFile.FileId} for document {document.Id}.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No files found for document {document.Id}");
                    }
                }

                Console.WriteLine("File download process completed.");
                //NOW LETS PROCESS THE DATA! LET'S CREATE THE REPORT
                
                // Generate CSV report (best for Power BI)
                await GenerateCsvReportAsync(reportResult);
                
                // Also generate Excel XLSX if needed
             //   await GenerateExcelReportAsync(reportResult);
                
                Console.WriteLine($"Report generated with {reportResult.Row.Count} rows.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void InitializeConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();
        }

        private static void InitializeAzureClients()
        {
            var credential = new DefaultAzureCredential();

            // Initialize Cosmos client
            var cosmosEndpoint = _configuration!["CosmosEndpoint"];
            _cosmosClient = new CosmosClient(cosmosEndpoint, credential);

            // Initialize Blob service client
            var storageAccountUrl = _configuration["StorageAccountUrl"];
            _blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl!), credential);

            Console.WriteLine("Azure clients initialized successfully.");
        }

        private static async Task<List<CosmosQueryResult>> QueryCosmosDBAsync()
        {
            Console.WriteLine("Querying CosmosDB...");

            var database = _cosmosClient!.GetDatabase("APIViewV2");
            var container = database.GetContainer("APIRevisions");

            var query = @"
                SELECT 
                    c.id as Id,
                    c.Files
                FROM c 
                WHERE c.CreatedOn >= ""2025-07-01T00:00:00Z"" 
                ";

            var queryDefinition = new QueryDefinition(query);
            var queryIterator = container.GetItemQueryIterator<CosmosQueryResult>(queryDefinition);

            var results = new List<CosmosQueryResult>();

            while (queryIterator.HasMoreResults)
            {
                var response = await queryIterator.ReadNextAsync();
                results.AddRange(response);
                
                Console.WriteLine($"Retrieved {response.Count} items from this page. Total RU consumed: {response.RequestCharge}");
            }

            return results;
        }

        private static async Task<ApiViewDocument?> DownloadFileFromBlobAsync(string documentId, string fileId)
        {
            try
            {
                Console.WriteLine($"Reading file {fileId} from document {documentId}...");

                // Get the container client
                var containerClient = _blobServiceClient!.GetBlobContainerClient("codefiles");

                // Construct the blob path: {documentId}/{fileId}
                var blobPath = $"{documentId}/{fileId}";
                var blobClient = containerClient.GetBlobClient(blobPath);

                // Check if blob exists
                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    Console.WriteLine($"Blob not found at path: {blobPath}");
                    return null;
                }

                // Get file properties
                var properties = await blobClient.GetPropertiesAsync();
                Console.WriteLine($"File size: {properties.Value.ContentLength} bytes");
                Console.WriteLine($"Content type: {properties.Value.ContentType}");
                Console.WriteLine($"Last modified: {properties.Value.LastModified}");

                // Read the content directly into memory
                var downloadResult = await blobClient.DownloadContentAsync();
                var jsonContent = downloadResult.Value.Content.ToString();

                Console.WriteLine($"Content read successfully from blob storage.");

                // Parse the JSON content directly
                var result = await ParseApiViewDocumentAsync(jsonContent, documentId, fileId);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {fileId}: {ex.Message}");
            }

            return null;
        }

        private static async Task<ApiViewDocument?> ParseApiViewDocumentAsync(string jsonContent, string documentId, string fileId)
        {
            try
            {
                Console.WriteLine($"Parsing APIView document: {fileId}");

                // Parse the JSON content
                var apiViewDoc = JsonSerializer.Deserialize<ApiViewDocument>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiViewDoc?.ReviewLines == null)
                {
                    Console.WriteLine("No ReviewLines found in the document.");
                    return null;
                }

                Console.WriteLine($"Found {apiViewDoc.ReviewLines.Count} review lines.");

                // Extract meaningful information
                var linesWithContent = apiViewDoc.ReviewLines
                    .Where(line => !string.IsNullOrEmpty(line.LineId) && line.Tokens.Any())
                    .ToList();

                Console.WriteLine($"Lines with content: {linesWithContent.Count}");

                // Display some sample information
                foreach (var line in linesWithContent.Take(3)) // Show first 3 lines with content
                {
                    var tokenValues = line.Tokens.Where(t => !string.IsNullOrEmpty(t.Value)).Select(t => t.Value);
                    var lineContent = string.Join(" ", tokenValues);
                    
                    Console.WriteLine($"  LineId: {line.LineId}");
                    if (!string.IsNullOrEmpty(line.CrossLanguageId))
                        Console.WriteLine($"    CrossLanguageId: {line.CrossLanguageId}");
                    if (!string.IsNullOrEmpty(line.RelatedToLine))
                        Console.WriteLine($"    RelatedToLine: {line.RelatedToLine}");
                    if (lineContent.Length > 0)
                        Console.WriteLine($"    Content: {lineContent.Substring(0, Math.Min(80, lineContent.Length))}...");
                    Console.WriteLine();
                }

                return apiViewDoc;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing APIView document {fileId}: {ex.Message}");
            }
            return null;
        }

        private static async Task SaveApiViewSummaryAsync(string documentId, string fileId, ApiViewDocument apiViewDoc)
        {
            try
            {
                var summary = new
                {
                    DocumentId = documentId,
                    FileId = fileId,
                    TotalLines = apiViewDoc.ReviewLines.Count,
                    LinesWithContent = apiViewDoc.ReviewLines.Count(l => !string.IsNullOrEmpty(l.LineId) && l.Tokens.Any()),
                    UniqueLineIds = apiViewDoc.ReviewLines.Where(l => !string.IsNullOrEmpty(l.LineId)).Select(l => l.LineId).Distinct().Count(),
                    CrossLanguageIds = apiViewDoc.ReviewLines.Where(l => !string.IsNullOrEmpty(l.CrossLanguageId)).Select(l => l.CrossLanguageId).Distinct().ToList(),
                    TokenKinds = apiViewDoc.ReviewLines.SelectMany(l => l.Tokens).Select(t => t.Kind).Distinct().OrderBy(k => k).ToList(),
                    SampleLines = apiViewDoc.ReviewLines
                        .Where(l => !string.IsNullOrEmpty(l.LineId) && l.Tokens.Any())
                        .Take(10)
                        .Select(l => new
                        {
                            l.LineId,
                            l.CrossLanguageId,
                            l.RelatedToLine,
                            Content = string.Join(" ", l.Tokens.Where(t => !string.IsNullOrEmpty(t.Value)).Select(t => t.Value))
                        })
                        .ToList()
                };

                var summaryPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads", $"{documentId}_{fileId}_summary.json");
                var summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(summaryPath, summaryJson);

                Console.WriteLine($"Summary saved to: {summaryPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving summary: {ex.Message}");
            }
        }

        private static async Task GenerateCsvReportAsync(Models.Report report)
        {
            try
            {
                Console.WriteLine("Generating CSV report...");

                var csv = new StringBuilder();
                
                // Header row
                csv.AppendLine("CreatedOn,ParserVersion,Language,APIRevisionId,PackageName,PackageVersion,HandwrittenLinesCount,TotalLines,HandwrittenPercentage");

                // Data rows
                foreach (var row in report.Row)
                {
                    // Calculate percentage of handwritten lines
                    var handwrittenPercentage = row.TotalLines > 0 ? Math.Round((double)row.HandwrittenLines / row.TotalLines * 100, 2) : 0;

                    csv.AppendLine($"{EscapeCsv(row.CreatedOn.ToString("o"))},{EscapeCsv(row.ParserVersion)},{EscapeCsv(row.Language)},{EscapeCsv(row.RevisionId)},{EscapeCsv(row.PackageName)},{EscapeCsv(row.PackageVersion)},{row.HandwrittenLines},{row.TotalLines},{handwrittenPercentage}");
                }
                
                var outputPath = Path.Combine("C:\\Repositories\\azure-sdk-tools\\src\\dotnet\\Report\\Result\\", $"APIView_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                await File.WriteAllTextAsync(outputPath, csv.ToString());

                Console.WriteLine($"CSV report saved to: {outputPath}");
                Console.WriteLine("You can import this CSV file directly into Power BI.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating CSV report: {ex.Message}");
            }
        }

        private static async Task GenerateExcelReportAsync(Models.Report report)
        {
            try
            {
                Console.WriteLine("Generating Excel XLSX report...");

                // Simple tab-delimited format that Excel can open
                var tsv = new StringBuilder();
                
                // Header row
                tsv.AppendLine("Parser Version\tLanguage\tAPI Revision Id\tPackage Name\tPackage Version\tHandwritten Lines Count\tTotal Lines");
                
                // Data rows
                foreach (var row in report.Row)
                {
                    tsv.AppendLine($"{EscapeTab(row.ParserVersion)}\t{EscapeTab(row.Language)}\t{EscapeTab(row.RevisionId)}\t{EscapeTab(row.PackageName)}\t{EscapeTab(row.PackageVersion)}\t{row.HandwrittenLines}\t{row.TotalLines}");
                }
                
                var outputPath = Path.Combine("C:\\Repositories\\azure-sdk-tools\\src\\dotnet\\Report\\Result\\", $"APIView_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                await File.WriteAllTextAsync(outputPath, tsv.ToString());

                Console.WriteLine($"Tab-delimited report saved to: {outputPath}");
                Console.WriteLine("You can open this file in Excel or import it into Power BI.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating Excel report: {ex.Message}");
            }
        }

        private static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
                
            // If the value contains comma, quote, or newline, wrap it in quotes and escape internal quotes
            if (input.Contains(',') || input.Contains('"') || input.Contains('\n') || input.Contains('\r'))
            {
                return $"\"{input.Replace("\"", "\"\"")}\"";
            }
            
            return input;
        }

        private static string EscapeTab(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
                
            return input
                .Replace("\t", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");
        }
    }
}
