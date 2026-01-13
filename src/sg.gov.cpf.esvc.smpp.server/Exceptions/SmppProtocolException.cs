using System.Diagnostics.CodeAnalysis;
using sg.gov.cpf.esvc.smpp.server.Constants;
using System.Runtime.Serialization;

namespace sg.gov.cpf.esvc.smpp.server.Exceptions;

[ExcludeFromCodeCoverage]
[Serializable]
public class SmppProtocolException : SmppException
{
    public uint? CommandId { get; }
    public uint? SequenceNumber { get; }

    public SmppProtocolException(string message) : base(message) { }

    public SmppProtocolException(uint errorCode, string message) : base(errorCode, message) { }

    public SmppProtocolException(uint commandId, uint sequenceNumber, string message) 
        : base(SmppConstants.SmppCommandStatus.ESME_RINVCMDID, message)
    {
        CommandId = commandId;
        SequenceNumber = sequenceNumber;
    }

    public SmppProtocolException(uint errorCode, uint commandId, uint sequenceNumber, string message) 
        : base(errorCode, message)
    {
        CommandId = commandId;
        SequenceNumber = sequenceNumber;
    }

    protected SmppProtocolException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        CommandId = info.GetUInt32(nameof(CommandId));
        SequenceNumber = info.GetUInt32(nameof(SequenceNumber));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(CommandId), CommandId);
        info.AddValue(nameof(SequenceNumber), SequenceNumber);
    }

}