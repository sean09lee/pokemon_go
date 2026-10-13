using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using TeamBuilder.Data.Constants;

namespace TeamBuilder.Extensions;

public static class AppConfigRegistration
{
    public static void AddAppConfiguration(this IServiceCollection _, IConfigurationManager configuration)
    {
        var connectionString = configuration.GetConnectionString(ServiceConstants.AppConfiguration);
        var configLabel = configuration[ServiceConstants.Label];
        var label = string.IsNullOrWhiteSpace(configLabel) ?
            ServiceConstants.Local.ToLower() :
            configLabel.ToLower() ;

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            configuration.AddAzureAppConfiguration(options =>
            {
                options.Connect(connectionString)
                    .Select(KeyFilter.Any, LabelFilter.Null)
                    .Select(KeyFilter.Any, label)
                    .ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    })
                    .ConfigureRefresh(refresh =>
                    {
                        refresh.Register(ServiceConstants.Refresh, refreshAll: true);
                        refresh.SetRefreshInterval(TimeSpan.FromMinutes(5));
                    });
            });
        }
    }
}