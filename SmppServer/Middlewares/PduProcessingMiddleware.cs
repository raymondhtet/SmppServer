using Smpp.Server.Interfaces;
using Smpp.Server.Models;

namespace Smpp.Server.Middlewares;

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