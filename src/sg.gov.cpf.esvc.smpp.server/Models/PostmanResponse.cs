namespace sg.gov.cpf.esvc.smpp.server.Models;

public class PostmanResponse
{
    public Error? Error { get; set; }

    public string? ID { get; set; }
}

public class Error
{
    public string? Code { get; set; }

    public string? Message { get; set; }

    public string? Type { get; set; }

    public string? ID { get; set; }
}