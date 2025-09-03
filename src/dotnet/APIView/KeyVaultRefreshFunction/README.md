# Key Vault Refresh Function

This Azure Function automatically updates the App Configuration sentinel value when Key Vault secrets change, triggering an automatic refresh in your APIView application without requiring restarts.

## 🏗️ Architecture

```
Key Vault Secret Change → Event Grid → Azure Function → Updates Sentinel → APIView App Refreshes
```

## 📋 Prerequisites

1. **Azure CLI** installed and logged in
2. **Azure Functions Core Tools** v4.x installed
3. **Key Vault** with secrets referenced in App Configuration
4. **App Configuration** instance with Key Vault references
5. **APIView app** running with the refresh configuration

## 🚀 Deployment Steps

### Step 1: Install Azure Functions Core Tools (if not installed)

```powershell
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

### Step 2: Deploy the Function

Run the deployment script with your Azure resources:

```powershell
.\deploy.ps1 -ResourceGroupName "your-rg" `
             -FunctionAppName "your-function-app" `
             -KeyVaultName "your-keyvault" `
             -AppConfigUrl "https://your-appconfig.azconfig.io" `
             -Location "East US"
```

### Step 3: Verify the Setup

1. **Check Function App**: Go to Azure Portal → Function Apps → Your Function App
2. **Verify Event Grid Subscription**: Go to Key Vault → Events → Event Subscriptions
3. **Test the Function**: Update a secret in Key Vault and check if the sentinel updates

## 🧪 Testing

### Test Key Vault Secret Update

1. Update a secret in your Key Vault:
   ```bash
   az keyvault secret set --vault-name "your-keyvault" --name "test-secret" --value "new-value"
   ```

2. Check the Function App logs:
   ```bash
   az functionapp logs tail --name "your-function-app" --resource-group "your-rg"
   ```

3. Verify App Configuration sentinel was updated:
   ```bash
   az appconfig kv show --name "your-appconfig" --key "Sentinel"
   ```

### Monitor Your APIView Application

- Your APIView app should automatically pick up the new Key Vault values within 5 minutes (based on your cache expiration)
- Check application logs for configuration refresh messages

## 🔧 Configuration Details

### Function App Settings

The function requires these application settings:

- `APPCONFIG_URL`: Your App Configuration endpoint URL
- Managed Identity with "App Configuration Data Owner" role

### Event Grid Events Monitored

- `Microsoft.KeyVault.SecretNewVersionCreated`
- `Microsoft.KeyVault.SecretUpdated`
- `Microsoft.KeyVault.SecretNearExpiry` (optional)

### APIView App Configuration

Your APIView app is already configured with:

```csharp
.ConfigureRefresh(refresh =>
{
    refresh.Register("Sentinel", refreshAll: true)
        .SetCacheExpiration(TimeSpan.FromMinutes(5));
});
```

## 🔍 Troubleshooting

### Function Not Triggering

1. **Check Event Grid Subscription**: Ensure it's created and active
2. **Verify Function App Permissions**: Managed identity needs App Configuration access
3. **Check Function Logs**: Look for error messages in Azure Portal

### App Not Refreshing

1. **Verify Middleware**: Ensure `app.UseAzureAppConfiguration()` is in Startup.cs
2. **Check Cache Expiration**: May take up to 5 minutes to refresh
3. **Validate Sentinel Value**: Confirm it's being updated in App Configuration

### Common Issues

- **403 Errors**: Check managed identity permissions
- **Function Timeout**: Increase timeout in host.json if needed
- **Network Issues**: Ensure Function App can reach App Configuration

## 📊 Monitoring

### Key Metrics to Monitor

- Function execution count and success rate
- Event Grid delivery success rate
- App Configuration access patterns
- Application configuration refresh frequency

### Azure Monitor Queries

```kusto
// Function executions
FunctionAppLogs
| where FunctionName == "UpdateSentinelOnKeyVaultChange"
| summarize count() by bin(TimeGenerated, 1h), Level

// Event Grid deliveries
EventGridTopicLogs
| where OperationName == "Microsoft.EventGrid/eventSubscriptions/deliverEvents"
| summarize count() by bin(TimeGenerated, 1h), ResultType
```

## 🚀 Benefits

- ✅ **Automatic**: No manual intervention required
- ✅ **Fast**: Near real-time updates (< 1 minute)
- ✅ **Reliable**: Built on Azure Event Grid
- ✅ **Scalable**: Serverless function scales automatically
- ✅ **Cost-effective**: Pay only for executions

## 🔄 How It Works

1. **Key Vault Change**: Someone updates a secret in Key Vault
2. **Event Grid Triggers**: Key Vault publishes an event to Event Grid
3. **Function Executes**: Azure Function receives the event and processes it
4. **Sentinel Updates**: Function updates the "Sentinel" value in App Configuration
5. **App Refreshes**: APIView app detects sentinel change and refreshes all configuration
6. **New Values Available**: Updated Key Vault values are now available in your app

This ensures your application always has the latest secrets without restarts or manual intervention!
