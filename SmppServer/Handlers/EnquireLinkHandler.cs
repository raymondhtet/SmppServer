using Smpp.Server.Constants;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Handlers;

public class EnquireLinkHandler(ILogger<EnquireLinkHandler> logger) : IPduHandler
{
    public Task<bool> CanHandle(SmppPdu pdu) 
        => Task.FromResult(pdu.CommandId == SmppConstants.SmppCommandId.EnquireLink);

    public Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        logger.LogInformation("Enquire link from {SystemId}", session.SystemId);

        var response = SmppResponseBuilder.Create()
            .AsEnquireLinkResponse(pdu.SequenceNumber)
            .Build();

        return Task.FromResult<SmppPdu?>(response);
    }

}