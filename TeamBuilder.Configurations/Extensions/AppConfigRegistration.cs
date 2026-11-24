using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using TeamBuilder.Data.Constants;

namespace TeamBuilder.Configurations.Extensions;

public static class AppConfigRegistration
{
    public static void AddAppConfiguration(this IServiceCollection _, IConfigurationManager configuration)
    {
        var connectionString = configuration.GetConnectionString(ServiceConstants.AppConfiguration);
        var configLabel = configuration.GetConnectionString(ServiceConstants.Label);
        var label = string.IsNullOrWhiteSpace(configLabel) ?
            ServiceConstants.Local.ToLower() :
            configLabel!.ToLower();
        var exclude = label.Equals(ServiceConstants.Local, StringComparison.CurrentCultureIgnoreCase);

        configuration.AddAzureAppConfiguration(options =>
        {
            options.Connect(connectionString)
                .ConfigureKeyVault(kv => kv.SetCredential(new DefaultAzureCredential()))
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