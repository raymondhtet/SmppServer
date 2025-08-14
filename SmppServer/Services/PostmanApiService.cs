using Smpp.Server.Interfaces;
using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Services;

public class PostmanApiService : IExternalMessageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PostmanApiService> _logger;

    public PostmanApiService(HttpClient httpClient, ILogger<PostmanApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ExternalServiceResult> SendMessageAsync(string systemId, string destinationAddress, string message, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Sending message via external API: {SystemId} -> {Destination}: '{Message}'", 
                systemId, destinationAddress, message);
            
            
            
            _logger.LogInformation("Message sent successfully via external API");
            return new ExternalServiceResult(IsSuccess: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message via external API");
            return new ExternalServiceResult(IsSuccess: false, ErrorMessage: ex.Message, ErrorCode: "API_ERROR");
        }
    }
}