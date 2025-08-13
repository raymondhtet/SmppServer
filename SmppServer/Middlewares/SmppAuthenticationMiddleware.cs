using Smpp.Server.Constants;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Middlewares;

public abstract class SmppAuthenticationMiddleware(ILogger<SmppAuthenticationMiddleware> logger)
    : PduProcessingMiddleware
{
    public override async Task<SmppPdu?> HandleAsync(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        // Skip authentication check for bind requests
        if (pdu.CommandId == SmppConstants.SmppCommandId.BindTransceiver)
            return await Next?.HandleAsync(pdu, session, cancellationToken)!;

        if (!session.IsAuthenticated)
        {
            logger.LogError("Unauthenticated request from session {SessionId}", session.Id);
            return SmppResponseBuilder.Create()
                .WithCommandId(pdu.CommandId | 0x80000000) // Response bit
                .WithSequenceNumber(pdu.SequenceNumber)
                .AsError(SmppConstants.SmppCommandStatus.ESME_RBINDFAIL)
                .Build();
        }

        return await Next?.HandleAsync(pdu, session, cancellationToken)!;
    }

}