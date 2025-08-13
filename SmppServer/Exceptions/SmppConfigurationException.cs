using System.Runtime.Serialization;

namespace Smpp.Server.Exceptions;

[Serializable]
public class SmppConfigurationException : SmppException
{
    public string? ConfigurationKey { get; }

    public SmppConfigurationException(string configurationKey, string message) : base(message)
    {
        ConfigurationKey = configurationKey;
    }

    public SmppConfigurationException(string configurationKey, string message, Exception innerException) 
        : base(message, innerException)
    {
        ConfigurationKey = configurationKey;
    }

    protected SmppConfigurationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        ConfigurationKey = info.GetString(nameof(ConfigurationKey));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ConfigurationKey), ConfigurationKey);
    }

}