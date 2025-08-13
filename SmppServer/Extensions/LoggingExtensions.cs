namespace Smpp.Server.Extensions;

public static partial class LoggingExtensions
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Session {SessionId} created for endpoint {Endpoint}")]
    public static partial void LogSessionCreated(this ILogger logger, string sessionId, string endpoint);
}