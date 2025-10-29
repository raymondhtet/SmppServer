using System.Diagnostics;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;

namespace Ssg.gov.cpf.esvc.smpp.server.Middlewares;

public class LoggingMiddleware(ILogger<LoggingMiddleware> logger) : PduProcessingMiddleware
{
    public override async Task<SmppPdu?> HandleAsync(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var result = await Next?.HandleAsync(pdu, session, cancellationToken)!;

        stopwatch.Stop();
        
        return result;
    }

}