using sg.gov.cpf.esvc.smpp.server.Models;

namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface IPduHandler
{
    Task<bool> CanHandle(SmppPdu pdu);
    Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken);
}