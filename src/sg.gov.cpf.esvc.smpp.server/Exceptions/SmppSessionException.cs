using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace sg.gov.cpf.esvc.smpp.server.Exceptions;

[ExcludeFromCodeCoverage]
[Serializable]
public class SmppSessionException : SmppException
{
    public string? SessionId { get; }

    public SmppSessionException(string sessionId, string message) : base(message)
    {
        SessionId = sessionId;
    }

    public SmppSessionException(string sessionId, string message, Exception innerException) : base(message, innerException)
    {
        SessionId = sessionId;
    }

    protected SmppSessionException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        SessionId = info.GetString(nameof(SessionId));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(SessionId), SessionId);
    }
}