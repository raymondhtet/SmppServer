using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;

namespace sg.gov.cpf.esvc.smpp.server.Handlers;

public class EnquireLinkHandler(ILogger<EnquireLinkHandler> logger) : IPduHandler
{
    public Task<bool> CanHandle(SmppPdu pdu) 
        => Task.FromResult(pdu.CommandId == SmppConstants.SmppCommandId.EnquireLink);

    public Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        //logger.LogInformation("Enquire link from {SystemId}", session.SystemId);

        var response = SmppResponseBuilder.Create()
            .AsEnquireLinkResponse(pdu.SequenceNumber)
            .Build();

        return Task.FromResult<SmppPdu?>(response);
    }

}