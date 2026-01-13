using System.Diagnostics.CodeAnalysis;

namespace sg.gov.cpf.esvc.smpp.server.Models.DTOs;

[ExcludeFromCodeCoverage]
public record BindTransceiverRequest(
    string ServiceType,
    string SystemId,
    string Password,
    string SystemType,
    byte InterfaceVersion,
    byte AddrTon,
    byte AddrNpi,
    string AddressRange);

[ExcludeFromCodeCoverage]
public record SubmitSmRequest
{
    public string ServiceType { get; init; } = "";
    public byte SourceAddrTon { get; init; }
    public byte SourceAddrNpi { get; init; }
    public string SourceAddress { get; init; } = "";
    public byte DestAddrTon { get; init; }
    public byte DestAddrNpi { get; init; }
    public string DestinationAddress { get; init; } = "";
    public byte EsmClass { get; init; }
    public byte ProtocolId { get; init; }
    public byte PriorityFlag { get; init; }
    public string ScheduleDeliveryTime { get; init; } = "";
    public string ValidityPeriod { get; init; } = "";
    public byte RegisteredDelivery { get; init; }
    public byte ReplaceIfPresentFlag { get; init; }
    public byte DataCoding { get; init; }
    public byte SmDefaultMsgId { get; init; }
    public byte[] ShortMessage { get; init; } = [];

    public byte[] MessagePayload { get; set; } = [];

    public string CampaignId { get; set; } = "";

    public int? DelayInSeconds { get; set; }
    public Dictionary<ushort, byte[]> OptionalParameters { get; set; } = new();
}

[ExcludeFromCodeCoverage]
public record ConcatenationResult(bool IsComplete, string? CompleteMessage = null);

[ExcludeFromCodeCoverage]
public record MessageProcessingResult(bool IsSuccess, string? ErrorMessage = null);

[ExcludeFromCodeCoverage]
public record ExternalServiceResult(bool IsSuccess = false, string? ErrorMessage = null, string? ErrorCode = null, string? ID = null);
