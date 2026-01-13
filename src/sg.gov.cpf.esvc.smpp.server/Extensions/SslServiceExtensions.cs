using System.Diagnostics.CodeAnalysis;
using sg.gov.cpf.esvc.smpp.server.BackgroundServices;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Services;

namespace sg.gov.cpf.esvc.smpp.server.Extensions;

[ExcludeFromCodeCoverage]
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

        // Replace regular SMPP server with enhanced version
        services.AddHostedService<SmppServer>();

        return services;
    }
}