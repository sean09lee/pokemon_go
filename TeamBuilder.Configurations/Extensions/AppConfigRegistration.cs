using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TeamBuilder.Data.Constants;

namespace TeamBuilder.Configurations.Extensions;

public static class AppConfigRegistration
{
    public static void AddAppConfiguration(this IServiceCollection _, IConfigurationManager configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString(ServiceConstants.AppConfiguration);
        var configLabel = configuration.GetConnectionString(ServiceConstants.Label);
        var label = string.IsNullOrWhiteSpace(configLabel) ?
            ServiceConstants.Local.ToLower() :
            configLabel!.ToLower();
        var exclude = label.Equals(ServiceConstants.Local, StringComparison.CurrentCultureIgnoreCase);

        // Only running locally do we skip Managed Identity/Environment/Workload-Identity
        // probes, since they just time out before falling through to
        // VisualStudioCredential/AzureCliCredential. In the deployed App Service,
        // IsDevelopment() is false, so ManagedIdentityCredential is tried and succeeds.
        var isLocal = environment.IsDevelopment();

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeManagedIdentityCredential = isLocal,
            ExcludeEnvironmentCredential = isLocal,
            ExcludeAzurePowerShellCredential = isLocal,
            ExcludeWorkloadIdentityCredential = isLocal,
        });

        configuration.AddAzureAppConfiguration(options =>
        {
            options.Connect(connectionString)
                .ConfigureKeyVault(kv => kv.SetCredential(credential))
                .Select(KeyFilter.Any, LabelFilter.Null)
                .Select(KeyFilter.Any, label)
                .ConfigureRefresh(refresh =>
                {
                    refresh.Register(ServiceConstants.Refresh, refreshAll: true);
                    refresh.SetRefreshInterval(TimeSpan.FromMinutes(5));
                });
        });
    }
}