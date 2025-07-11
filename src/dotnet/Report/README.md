# APIView File Downloader

This console application queries Azure CosmosDB and downloads files from Azure Storage based on the specified criteria.

## Features

- Queries the `apiview-cosmos` CosmosDB account on database `APIView2`
- Filters documents created after `2025-07-10T00:00:00Z`
- Finds the latest file by CreationDate for each document
- Downloads files from the `apiview` storage account's `codefiles` container
- Uses Azure DefaultAzureCredential for authentication

## Configuration

The application uses `appsettings.json` for configuration:

```json
{
  "CosmosEndpoint": "https://apiview-cosmos.documents.azure.com:443/",
  "StorageAccountUrl": "https://apiview.blob.core.windows.net/"
}
```

## Authentication

The application uses Azure's `DefaultAzureCredential` which will attempt to authenticate using:
1. Environment variables
2. Managed Identity (if running on Azure)
3. Visual Studio
4. Azure CLI
5. Azure PowerShell

Make sure you're logged in via Azure CLI or have the appropriate permissions set up.

## Usage

1. Ensure you have the necessary Azure permissions to access:
   - CosmosDB account: `apiview-cosmos`
   - Storage account: `apiview`

2. Run the application:
   ```
   dotnet run
   ```

3. Downloaded files will be saved to the `downloads` directory in the application's working directory.

## Output

The application will:
- Display progress information as it queries CosmosDB
- Show which documents and files are being processed
- Download files with naming pattern: `{documentId}_{fileId}`
- Display file information (size, content type, last modified)

## Error Handling

The application includes error handling for:
- Configuration issues
- Azure authentication problems
- CosmosDB query failures
- Blob storage access issues
- File download errors
