using System.Diagnostics.CodeAnalysis;

namespace sg.gov.cpf.esvc.smpp.server.Models;

[ExcludeFromCodeCoverage]
public class DeliveryStatusMapping
{
    public string MessageState { get; set; } = "";
    public string ErrorStatus { get; set; } = "";
    public string ErrorCode { get; set; } = "";

}