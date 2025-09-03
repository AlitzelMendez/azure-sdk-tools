using System;
using System.Threading.Tasks;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;

namespace KeyVaultRefreshFunction;

public static class KeyVaultEventHandler
{
    [FunctionName("UpdateSentinelOnKeyVaultChange")]
    public static async Task Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
    {
        try
        {
            log.LogInformation("Received Event Grid event: {EventType}", eventGridEvent.EventType);
            if (IsRelevantKeyVaultEvent(eventGridEvent.EventType))
            {
                log.LogInformation("Processing Key Vault secret change event");

                string appConfigUrl = Environment.GetEnvironmentVariable("APPCONFIG_URL");
                if (string.IsNullOrEmpty(appConfigUrl))
                {
                    log.LogError("APPCONFIG_URL environment variable is not set");
                    return;
                }

                ConfigurationClient appConfigClient = new(
                    new Uri(appConfigUrl),
                    new DefaultAzureCredential());

                string sentinelValue = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");

                await appConfigClient.SetConfigurationSettingAsync(
                    new ConfigurationSetting("Sentinel", sentinelValue));

                log.LogInformation("Successfully updated Sentinel value to: {SentinelValue}", sentinelValue);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error processing Key Vault event");
            throw;
        }
    }

    private static bool IsRelevantKeyVaultEvent(string eventType)
    {
        return eventType == "Microsoft.KeyVault.SecretNewVersionCreated" ||
               eventType == "Microsoft.KeyVault.SecretUpdated" ||
               eventType == "Microsoft.KeyVault.SecretNearExpiry";
    }
}
