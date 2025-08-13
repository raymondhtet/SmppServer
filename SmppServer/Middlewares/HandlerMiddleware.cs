using Smpp.Server.Constants;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Middlewares;

public class HandlerMiddleware(List<IPduHandler> handlers, ILogger<HandlerMiddleware> logger)
    : PduProcessingMiddleware
{
    public override async Task<SmppPdu?> HandleAsync(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            if (await handler.CanHandle(pdu))
            {
                logger.LogDebug("Using handler {HandlerType} for PDU {CommandId}", 
                    handler.GetType().Name, pdu.CommandId);
                
                return await handler.Handle(pdu, session, cancellationToken);
            }
        }

        logger.LogWarning("No handler found for PDU command {CommandId}", pdu.CommandId);
        
        return SmppResponseBuilder.Create()
            .WithCommandId(pdu.CommandId | 0x80000000)
            .WithSequenceNumber(pdu.SequenceNumber)
            .AsError(SmppConstants.SmppCommandStatus.ESME_RINVCMDID)
            .Build();
    }

}