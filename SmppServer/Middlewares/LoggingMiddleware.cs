using System.Diagnostics;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Middlewares;

public class LoggingMiddleware(ILogger<LoggingMiddleware> logger) : PduProcessingMiddleware
{
    public override async Task<SmppPdu?> HandleAsync(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        logger.LogInformation("Processing PDU {CommandId} from {SystemId} (Seq: {SequenceNumber})", 
            pdu.CommandId, session.SystemId ?? "Unknown", pdu.SequenceNumber);

        var result = await Next?.HandleAsync(pdu, session, cancellationToken)!;

        stopwatch.Stop();
        logger.LogInformation("PDU {CommandId} processed in {ElapsedMs}ms", 
            pdu.CommandId, stopwatch.ElapsedMilliseconds);

        return result;
    }

}