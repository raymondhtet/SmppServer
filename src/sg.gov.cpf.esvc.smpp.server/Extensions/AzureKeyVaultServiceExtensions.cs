using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Services;

namespace sg.gov.cpf.esvc.smpp.server.Extensions
{
    public static class AzureKeyVaultServiceExtensions
    {
        public static void AddAzureKeyVaultFramework(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AzureKeyVaultConfiguration>(configuration.GetSection(nameof(AzureKeyVaultConfiguration)));

            services.AddSingleton<SecretClient>(serviceProvider =>
            {
                var keyVaultConfig = serviceProvider.GetRequiredService<IOptions<AzureKeyVaultConfiguration>>().Value;

                var envConfig = serviceProvider.GetRequiredService<EnvironmentVariablesConfiguration>();

                var credential = new DefaultAzureCredential();

                var options = new SecretClientOptions();

                if (keyVaultConfig.RetryOptions != null)
                {
                    options.Retry.Delay = TimeSpan.FromSeconds(keyVaultConfig.RetryOptions.Delay);
                    options.Retry.MaxDelay = TimeSpan.FromSeconds(keyVaultConfig.RetryOptions.MaxDelay);
                    options.Retry.MaxRetries = keyVaultConfig.RetryOptions.MaxRetries;
                    options.Retry.Mode = Enum.Parse<RetryMode>(keyVaultConfig.RetryOptions.Mode);
                }

                return new SecretClient(new Uri(envConfig.KeyVaultUri), credential, options);
            });

            services.AddSingleton<IKeyVaultService, AzureKeyVaultService>();
        }
    }
}
