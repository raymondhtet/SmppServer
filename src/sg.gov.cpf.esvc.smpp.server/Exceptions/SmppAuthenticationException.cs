using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Exceptions;

namespace sg.gov.cpf.esvc.sg.gov.cpf.esvc.smpp.server.Exceptions;

[ExcludeFromCodeCoverage]
[Serializable]
public class SmppAuthenticationException : SmppException
{
    public string? AttemptedSystemId { get; }

    public SmppAuthenticationException(string systemId)
        : base(SmppConstants.SmppCommandStatus.ESME_RBINDFAIL, $"Authentication failed for system ID: {systemId}")
    {
        AttemptedSystemId = systemId;
    }

    public SmppAuthenticationException(string systemId, string message)
        : base(SmppConstants.SmppCommandStatus.ESME_RBINDFAIL, message)
    {
        AttemptedSystemId = systemId;
    }

    protected SmppAuthenticationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        AttemptedSystemId = info.GetString(nameof(AttemptedSystemId));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(AttemptedSystemId), AttemptedSystemId);
    }
}