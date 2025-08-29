# Testing the New Endpoints

## Testing the AutoReviewController.CheckDuplicateLineIds endpoint

You can test the new `CheckDuplicateLineIds` endpoint in the `AutoReviewController` using these approaches:

### 1. Using curl (Command Line)

```bash
# Test with a file upload
curl -X POST \
  -H "Authorization: ApiKey YOUR_API_KEY" \
  -F "file=@path/to/your/apiview-file.json" \
  "https://your-apiview-domain/AutoReview/CheckDuplicateLineIds"
```

### 2. Using PowerShell

```powershell
# Create a form with the file
$form = @{
    file = Get-Item "C:\path\to\your\apiview-file.json"
}

# Make the request
$response = Invoke-RestMethod -Uri "https://your-apiview-domain/AutoReview/CheckDuplicateLineIds" `
    -Method Post `
    -Form $form `
    -Headers @{ "Authorization" = "ApiKey YOUR_API_KEY" }

# Display the results
$response | ConvertTo-Json -Depth 3
```

### 3. Expected Response

```json
{
  "fileName": "apiview-file.json",
  "packageName": "azure-storage-blob",
  "language": "Python",
  "hasDuplicateLineIds": true,
  "duplicateLineIds": "line123, line456",
  "totalLines": 1500,
  "linesWithIds": 1450
}
```

## Testing the ReportController endpoints

### 1. Generate JSON Report

```bash
curl -X GET \
  -H "Authorization: ApiKey YOUR_API_KEY" \
  "https://your-apiview-domain/Report/GenerateReport?language=Python&maxResults=10"
```

### 2. Generate CSV Report

```bash
curl -X GET \
  -H "Authorization: ApiKey YOUR_API_KEY" \
  -o "apiview-report.csv" \
  "https://your-apiview-domain/Report/GenerateCsvReport?language=Python&maxResults=10"
```

### 3. Check file for duplicates

```bash
curl -X POST \
  -H "Authorization: ApiKey YOUR_API_KEY" \
  -F "file=@path/to/your/apiview-file.json" \
  "https://your-apiview-domain/Report/CheckDuplicateLineIds"
```

## Configuration Requirements

Make sure your `appsettings.json` or environment variables include:

```json
{
  "CosmosEndpoint": "https://your-cosmos-account.documents.azure.com:443/",
  "StorageAccountUrl": "https://yourstorageaccount.blob.core.windows.net/"
}
```

## Authentication

Both controllers use the `ApiKeyAuthorizeAsyncFilter`, so you'll need to:

1. Include the proper API key in your request headers
2. Ensure the API key is configured in your APIView instance
3. Use the format: `Authorization: ApiKey YOUR_API_KEY`

## Error Responses

If something goes wrong, you'll get detailed error information:

```json
{
  "error": "Detailed error message",
  "stackTrace": "Full stack trace for debugging"
}
```

## Local Development Testing

If running locally, you can test against `https://localhost:5001` (or your local port):

```bash
curl -k -X POST \
  -H "Authorization: ApiKey YOUR_LOCAL_API_KEY" \
  -F "file=@test-file.json" \
  "https://localhost:5001/AutoReview/CheckDuplicateLineIds"
```

Note the `-k` flag to ignore SSL certificate warnings in local development.
