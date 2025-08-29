# ReportController API Documentation

The `ReportController` provides endpoints for generating reports about APIView documents, analyzing duplicate line IDs, and downloading CSV reports.

## Endpoints

### 1. Generate Report
**GET** `/Report/GenerateReport`

Generates a report analyzing APIView documents from CosmosDB and blob storage.

#### Parameters:
- `language` (optional): Filter by specific language (e.g., "Python", "Java", "C#")
- `startDate` (optional): Filter documents created after this date (ISO format: 2025-01-01T00:00:00Z)
- `maxResults` (optional): Maximum number of results to return (default: 100)

#### Example:
```
GET /Report/GenerateReport?language=Python&startDate=2025-01-01T00:00:00Z&maxResults=50
```

#### Response:
```json
{
  "message": "Report generated with 25 rows.",
  "data": [
    {
      "revisionId": "abc123",
      "packageName": "azure-storage-blob",
      "packageVersion": "12.19.0",
      "parserVersion": "0.3.21",
      "language": "Python",
      "totalLines": 1500,
      "handwrittenLines": 1200,
      "createdOn": "2025-01-15T10:30:00Z",
      "duplicateLineIds": "line1, line2"
    }
  ]
}
```

### 2. Generate CSV Report
**GET** `/Report/GenerateCsvReport`

Same as GenerateReport but returns the data as a downloadable CSV file.

#### Parameters:
Same as GenerateReport endpoint.

#### Example:
```
GET /Report/GenerateCsvReport?language=Python&maxResults=100
```

#### Response:
Downloads a CSV file named `APIView_Report_YYYYMMDD_HHMMSS.csv` with columns:
- CreatedOn
- ParserVersion
- Language
- APIRevisionId
- PackageName
- PackageVersion
- HandwrittenLinesCount
- TotalLines
- HandwrittenPercentage
- DuplicateLineIds

### 3. Check Duplicate Line IDs
**POST** `/Report/CheckDuplicateLineIds`

Analyzes an uploaded file for duplicate line IDs.

#### Parameters:
- `file` (form data): The APIView file to analyze

#### Example:
```bash
curl -X POST \
  -H "Content-Type: multipart/form-data" \
  -F "file=@myfile.json" \
  "https://your-api-domain/Report/CheckDuplicateLineIds"
```

#### Response:
```json
{
  "fileName": "myfile.json",
  "hasDuplicateLineIds": true,
  "duplicateLineIds": "line1, line2, line3",
  "totalLines": 1000,
  "linesWithIds": 950
}
```

## Authentication

All endpoints require API key authentication using the `ApiKeyAuthorizeAsyncFilter`. Include your API key in the request headers.

## Configuration

The controller requires the following configuration settings:
- `CosmosEndpoint`: The endpoint URL for your Cosmos DB
- `StorageAccountUrl`: The URL for your Azure Storage Account

These should be configured in your `appsettings.json` or through environment variables.

## Error Handling

All endpoints return appropriate HTTP status codes:
- `200 OK`: Success
- `400 Bad Request`: Invalid parameters or missing file
- `401 Unauthorized`: Missing or invalid API key
- `500 Internal Server Error`: Server error with details in response

Error responses include an error message and stack trace for debugging:
```json
{
  "error": "Error message here",
  "stackTrace": "Stack trace details..."
}
```

## Use Cases

1. **Generate reports for Power BI**: Use the CSV endpoint to get data that can be imported into Power BI for visualization
2. **Monitor API quality**: Check for duplicate line IDs which indicate potential issues in API generation
3. **Track parser improvements**: Monitor handwritten vs total lines ratio across different parser versions
4. **Language-specific analysis**: Filter by language to analyze specific SDK languages

## Integration with the Console Application

This controller provides the same functionality as the standalone console application in `src/dotnet/Report/Program.cs`, but exposed as web API endpoints for integration with other systems.
