namespace Smpp.Server.Models;

public class PostmanApiResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? MessageState { get; set; }
    public string? ErrorStatus { get; set; }

}