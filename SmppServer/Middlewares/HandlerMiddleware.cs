using Smpp.Server.Constants;
using Smpp.Server.Handlers;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Middlewares;

public class HandlerMiddleware(IServiceProvider serviceProvider, ILogger<HandlerMiddleware> logger)
    : PduProcessingMiddleware
{
    public override async Task<SmppPdu?> HandleAsync(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        // Create a scope for this request to resolve scoped services
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        // Get handlers from DI container using scoped provider
        var handlers = new List<IPduHandler>
        {
            scopedProvider.GetRequiredService<BindTransceiverHandler>(),
            scopedProvider.GetRequiredService<SubmitSmHandler>(),
            scopedProvider.GetRequiredService<EnquireLinkHandler>(),
            scopedProvider.GetRequiredService<UnbindHandler>()
        };
        
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