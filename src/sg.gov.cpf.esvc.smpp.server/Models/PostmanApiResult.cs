namespace sg.gov.cpf.esvc.smpp.server.Models;

public class PostmanApiResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? MessageState { get; set; }
    public string? ErrorStatus { get; set; }

    public string? ErrorStackTrace { get; set; }

    public string? ID { get; set; }
}