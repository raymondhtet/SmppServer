using System.Diagnostics.CodeAnalysis;
using static sg.gov.cpf.esvc.smpp.server.Constants.SmppConstants;

namespace sg.gov.cpf.esvc.smpp.server.Models;

[ExcludeFromCodeCoverage]
public class DeliveryStatus
{
    public MessageState MessageState { get; set; }
    public string? ErrorStatus { get; set; }
    public string? ErrorCode { get; set; }
}