# Deploy Key Vault Refresh Function and Event Grid Subscription
# This script will help you deploy the Azure Function and set up Event Grid

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$FunctionAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$KeyVaultName,
    
    [Parameter(Mandatory=$true)]
    [string]$AppConfigUrl,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

Write-Host "🚀 Starting deployment of Key Vault Refresh Function..." -ForegroundColor Green

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
}

# Create or update the Function App
Write-Host "📦 Creating/updating Function App: $FunctionAppName" -ForegroundColor Yellow
az functionapp create `
    --resource-group $ResourceGroupName `
    --consumption-plan-location $Location `
    --runtime dotnet `
    --functions-version 4 `
    --name $FunctionAppName `
    --storage-account $FunctionAppName"storage" `
    --assign-identity

# Get the Function App's managed identity
$functionAppIdentity = az functionapp identity show --name $FunctionAppName --resource-group $ResourceGroupName --query principalId -o tsv

Write-Host "🔑 Function App Managed Identity: $functionAppIdentity" -ForegroundColor Cyan

# Set App Configuration URL in Function App settings
Write-Host "⚙️ Setting App Configuration URL..." -ForegroundColor Yellow
az functionapp config appsettings set `
    --name $FunctionAppName `
    --resource-group $ResourceGroupName `
    --settings "APPCONFIG_URL=$AppConfigUrl"

# Grant the Function App access to App Configuration
Write-Host "🔐 Granting App Configuration access..." -ForegroundColor Yellow
$appConfigResourceId = az appconfig show --name ($AppConfigUrl -replace 'https://|\.azconfig\.io', '') --query id -o tsv
az role assignment create `
    --assignee $functionAppIdentity `
    --role "App Configuration Data Owner" `
    --scope $appConfigResourceId

# Deploy the function code
Write-Host "📤 Deploying function code..." -ForegroundColor Yellow
Push-Location -Path $PSScriptRoot
func azure functionapp publish $FunctionAppName
Pop-Location

# Get Key Vault resource ID
$keyVaultResourceId = az keyvault show --name $KeyVaultName --query id -o tsv

# Create Event Grid subscription
$eventSubscriptionName = "$KeyVaultName-to-$FunctionAppName"
Write-Host "🔔 Creating Event Grid subscription: $eventSubscriptionName" -ForegroundColor Yellow

# Get the function key for the Event Grid subscription
$functionKey = az functionapp function keys list `
    --function-name "UpdateSentinelOnKeyVaultChange" `
    --name $FunctionAppName `
    --resource-group $ResourceGroupName `
    --query "default" -o tsv

$functionEndpoint = "https://$FunctionAppName.azurewebsites.net/runtime/webhooks/eventgrid?functionName=UpdateSentinelOnKeyVaultChange&code=$functionKey"

az eventgrid event-subscription create `
    --name $eventSubscriptionName `
    --source-resource-id $keyVaultResourceId `
    --endpoint-type webhook `
    --endpoint $functionEndpoint `
    --included-event-types "Microsoft.KeyVault.SecretNewVersionCreated" "Microsoft.KeyVault.SecretUpdated"

Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Summary:" -ForegroundColor Cyan
Write-Host "  • Function App: $FunctionAppName" -ForegroundColor White
Write-Host "  • Key Vault: $KeyVaultName" -ForegroundColor White
Write-Host "  • Event Subscription: $eventSubscriptionName" -ForegroundColor White
Write-Host "  • App Config: $AppConfigUrl" -ForegroundColor White
Write-Host ""
Write-Host "🔄 Your setup is now complete! When Key Vault secrets change, your app will automatically refresh." -ForegroundColor Green
