using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;

namespace Ssg.gov.cpf.esvc.smpp.server.Middlewares;

public abstract class PduProcessingMiddleware
{
    protected PduProcessingMiddleware? Next;

    public PduProcessingMiddleware SetNext(PduProcessingMiddleware next)
    {
        Next = next;
        return next;
    }

    public abstract Task<SmppPdu?> HandleAsync(SmppPdu pdu, ISmppSession session, CancellationToken cancellationToken);
}