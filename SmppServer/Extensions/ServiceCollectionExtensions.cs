using Smpp.Server.Models.AppSettings;
using Smpp.Server.BackgroundServices;
namespace Smpp.Server.Extensions
{
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
}
