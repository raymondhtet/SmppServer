using Smpp.Server.Models;

namespace Smpp.Server.Interfaces;

public interface IPduHandler
{
    Task<bool> CanHandle(SmppPdu pdu);
    Task<SmppPdu?> Handle(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken);
}