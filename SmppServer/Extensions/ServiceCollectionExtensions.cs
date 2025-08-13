using Smpp.Server.BackgroundServices;
using Smpp.Server.Configurations;

namespace Smpp.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSmppServer(
        this IServiceCollection services,
        IConfiguration configure)
    {
        services.Configure<SmppServerConfiguration>(configure);

        services.AddHostedService<SmppServer>();
        //services.AddHttpClient<IPostmanApiService, PostmanApiService>();

        return services;
    }
}