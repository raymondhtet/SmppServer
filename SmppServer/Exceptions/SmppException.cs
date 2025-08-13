using System.Runtime.Serialization;

namespace Smpp.Server.Exceptions;

public class SmppException : Exception
{
    public uint ErrorCode { get; }
    public string? SystemId { get; }

    public SmppException() : base() { }
    
    public SmppException(string message) : base(message) { }
    
    public SmppException(string message, Exception innerException) : base(message, innerException) { }
    
    public SmppException(uint errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
    
    public SmppException(uint errorCode, string message, string? systemId) : base(message)
    {
        ErrorCode = errorCode;
        SystemId = systemId;
    }

    protected SmppException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        ErrorCode = info.GetUInt32(nameof(ErrorCode));
        SystemId = info.GetString(nameof(SystemId));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ErrorCode), ErrorCode);
        info.AddValue(nameof(SystemId), SystemId);
    }

}