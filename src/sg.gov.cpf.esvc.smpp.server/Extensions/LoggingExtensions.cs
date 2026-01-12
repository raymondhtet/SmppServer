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
        logging.AddFilter<ApplicationInsightsLoggerProvider>("", environment.MinimumLogLevel);

        return logging;
    }

}