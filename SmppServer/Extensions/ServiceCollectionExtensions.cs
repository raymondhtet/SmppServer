using Smpp.Server.BackgroundServices;
using Smpp.Server.Configurations;
using Smpp.Server.Handlers;
using Smpp.Server.Helpers;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;
using Smpp.Server.Services;

namespace Smpp.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSmppServer(
        this IServiceCollection services,
        IConfiguration configure)
    {
        services.Configure<SmppServerConfiguration>(configure.GetSection(nameof(SmppServerConfiguration)));
        
        services.Configure<PostmanApiConfiguration>(configure.GetSection(nameof(PostmanApiConfiguration)));

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