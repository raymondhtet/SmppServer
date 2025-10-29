using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;

namespace sg.gov.cpf.esvc.smpp.server.Handlers;

public class UnbindHandler(ILogger<EnquireLinkHandler> logger) : IPduHandler
{
    public Task<bool> CanHandle(SmppPdu pdu) 
        => Task.FromResult(pdu.CommandId == SmppConstants.SmppCommandId.Unbind);
    public Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken)
    {
        logger.LogInformation("{SystemId} is unbinding", session.SystemId);

        var response = SmppResponseBuilder.Create()
            .AsUnbindResponse(pdu.SequenceNumber)
            .Build();

        // Schedule session close after sending response
        _ = Task.Run(async () =>
        {
            await Task.Delay(100, cancellationToken);
            session.Close();
        }, cancellationToken);

        return Task.FromResult<SmppPdu?>(response);
    }
}