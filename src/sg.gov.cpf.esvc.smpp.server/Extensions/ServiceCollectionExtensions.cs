using System.Diagnostics.CodeAnalysis;
using sg.gov.cpf.esvc.sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.BackgroundServices;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Handlers;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Services;

namespace sg.gov.cpf.esvc.smpp.server.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSmppServer(
        this IServiceCollection services,
        IConfiguration configure)
    {
        services.Configure<SmppServerConfiguration>(configure.GetSection(nameof(SmppServerConfiguration)));
        
        services.Configure<PostmanApiConfiguration>(configure.GetSection(nameof(PostmanApiConfiguration)));

        services.Configure<WhitelistedSmsConfiguration>(configure.GetSection(nameof(WhitelistedSmsConfiguration)));

        services.AddHostedService<SmppServer>();
        services.AddSingleton<MessageTracker>();
        services.AddScoped<IAuthenticationService, ConfigurationAuthenticationService>();
        services.AddScoped<IMessageConcatenationService, MessageConcatenationService>();
        services.AddScoped<IMessageProcessor, MessageProcessor>();
        services.AddScoped<IDeliveryReceiptSender, DeliveryReceiptSender>();

        services.AddHttpClient<IExternalMessageService, PostmanApiService>();
        
        services.AddScoped<BindTransceiverHandler>();
        services.AddScoped<SubmitSmHandler>();
        services.AddScoped<EnquireLinkHandler>();
        services.AddScoped<UnbindHandler>();


        return services;
    }
}