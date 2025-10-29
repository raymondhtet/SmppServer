using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using sg.gov.cpf.esvc.smpp.server.Configurations;

namespace sg.gov.cpf.esvc.smpp.server.Extensions;

public static partial class LoggingExtensions
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Session {SessionId} created for endpoint {Endpoint}")]
    public static partial void LogSessionCreated(this ILogger logger, string sessionId, string endpoint);

    public static ILoggingBuilder ConfigureLogging(this ILoggingBuilder logging, EnvironmentVariablesConfiguration environment)
    {
        logging.SetMinimumLevel(environment.MinimumLogLevel);

        // Console logging
        logging.AddFilter("Microsoft", environment.MinimumLogLevel)
               .AddFilter("Microsoft.Hosting.Lifetime", environment.MinimumLogLevel);

        // Application Insights logging
        logging.AddApplicationInsights()
               .AddFilter<ApplicationInsightsLoggerProvider>("Default", environment.MinimumLogLevel)
               .AddFilter<ApplicationInsightsLoggerProvider>("Microsoft", environment.MinimumLogLevel)
               .AddFilter<ApplicationInsightsLoggerProvider>("Microsoft.Hosting.Lifetime", environment.MinimumLogLevel);

        return logging;
    }

}