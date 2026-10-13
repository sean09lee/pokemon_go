using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using TeamBuilder.Data.Constants;

namespace TeamBuilder.Extensions;

public static class AppConfigRegistration
{
    public static void AddAppConfiguration(this IServiceCollection services, IConfigurationManager configuration)
    {
        var connectionString = configuration.GetConnectionString(ServiceConstants.AppConfiguration);
        var label = configuration[ServiceConstants.Label] ?? ServiceConstants.Local;

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
                        refresh.Register("Refresh", refreshAll: true);
                        refresh.SetCacheExpiration(TimeSpan.FromMinutes(5));
                    });
            });
        }
    }
}