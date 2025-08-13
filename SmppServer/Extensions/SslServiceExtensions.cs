using Smpp.Server.BackgroundServices;
using Smpp.Server.Configurations;
using Smpp.Server.Interfaces;
using Smpp.Server.Services;

namespace Smpp.Server.Extensions;

public static class SslServiceExtensions
{
    /// <summary>
    /// Add SSL/TLS support to SMPP server
    /// </summary>
    public static IServiceCollection AddSmppServerWithSsl(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add base SMPP server
        services.AddSmppServer(configuration);

        // Add SSL configuration
        services.Configure<SslConfiguration>(
            configuration.GetSection(nameof(SslConfiguration)));
        
        // Add SSL certificate manager
        services.AddSingleton<ISslCertificateManager, SslCertificateManager>();

        services.Configure<SmppServerConfiguration>(configuration);
        
        // Replace regular SMPP server with enhanced version
        services.AddHostedService<SmppServer>();

        return services;
    }
}