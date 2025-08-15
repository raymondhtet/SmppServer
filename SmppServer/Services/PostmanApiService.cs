using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Smpp.Server.Configurations;
using Smpp.Server.Interfaces;
using Smpp.Server.Models;
using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Services;

public class PostmanApiService(
    HttpClient httpClient,
    ILogger<PostmanApiService> logger,
    IOptions<PostmanApiConfiguration> configuration)
    : IExternalMessageService
{
    private readonly PostmanApiConfiguration _apiConfiguration = configuration.Value;

    public async Task<ExternalServiceResult> SendMessageAsync(string systemId, string destinationAddress, string message, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Sending message via Postman API: {SystemId} -> {Destination}: '{Message}'", 
                systemId, destinationAddress, message);

            // Get configuration values
            var postmanBaseUrl = _apiConfiguration.PostmanBaseUrl;
            if (string.IsNullOrEmpty(postmanBaseUrl))
            {
                logger.LogError("PostmanBaseUrl configuration is missing");
                return new ExternalServiceResult(IsSuccess: false, ErrorMessage: "Configuration error", ErrorCode: "CONFIG_ERROR");
            }
            
            // Get configuration values
            var singleSmsUrl = _apiConfiguration.SingleSmsUrl;
            if (string.IsNullOrEmpty(singleSmsUrl))
            {
                logger.LogError("singleSmsUrl configuration is missing");
                return new ExternalServiceResult(IsSuccess: false, ErrorMessage: "Configuration error", ErrorCode: "CONFIG_ERROR");
            }

            // Use systemId as campaignId and get API key from configuration or use systemId
            var campaignId = _apiConfiguration.CampaignId;
            var apiKey = _apiConfiguration.ApiKey; 

            // build postman url
            var postmanFullUrl = $"{postmanBaseUrl}/{string.Format(singleSmsUrl, campaignId)}";
            
            var result = await SendMessageToPostmanApi(apiKey, destinationAddress, message, "english", postmanFullUrl, cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Message sent successfully via Postman API");
                return new ExternalServiceResult(IsSuccess: true);
            }
            else
            {
                logger.LogError("Failed to send message via Postman API: {ErrorMessage}", result.ErrorMessage);
                return new ExternalServiceResult(IsSuccess: false, ErrorMessage: result.ErrorMessage, ErrorCode: result.ErrorCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while sending message via Postman API");
            return new ExternalServiceResult(IsSuccess: false, ErrorMessage: ex.Message, ErrorCode: "API_ERROR");
        }
    }

    /// <summary>
    /// Send message to Postman API (converted from JavaScript sendMessage function)
    /// </summary>
    private async Task<PostmanApiResult> SendMessageToPostmanApi(
        string apiKey,
        string recipient,
        string messageBody,
        string language,
        string postmanFullUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Create headers for request
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // 2. Create request body
            var requestBody = new
            {
                recipient = recipient,
                language = language,
                values = new
                {
                    body = messageBody
                }
            };

            var jsonBody = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // 3. Build the URL
            
            logger.LogInformation("Posting to Postman API: {Url}", postmanFullUrl);
            logger.LogInformation("Request body: {RequestBody}", jsonBody);

            // 4. Make request
            var response = await httpClient.PostAsync(postmanFullUrl, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogDebug("Postman API response status: {StatusCode}", response.StatusCode);
            logger.LogDebug("Postman API response: {ResponseContent}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully called Postman POST Message API: {Response}", responseContent);
                return new PostmanApiResult { IsSuccess = true };
            }
            else
            {
                // Parse error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<PostmanErrorResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var errorMapping = GetDeliverSmStatusForApiError(errorResponse);
                    
                    logger.LogWarning("Postman API returned error: {ErrorCode} - {ErrorMessage}", 
                        errorResponse?.Code, errorResponse?.Message);

                    return new PostmanApiResult 
                    { 
                        IsSuccess = false, 
                        ErrorMessage = errorResponse?.Message ?? "Unknown error",
                        ErrorCode = errorMapping.ErrorCode,
                        MessageState = errorMapping.MessageState,
                        ErrorStatus = errorMapping.ErrorStatus
                    };
                }
                catch (JsonException)
                {
                    logger.LogError("Failed to parse Postman API error response: {ResponseContent}", responseContent);
                    return new PostmanApiResult 
                    { 
                        IsSuccess = false, 
                        ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}",
                        ErrorCode = "005"
                    };
                }
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error occurred while calling Postman API");
            return new PostmanApiResult 
            { 
                IsSuccess = false, 
                ErrorMessage = ex.Message,
                ErrorCode = "005"
            };
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Postman API request timed out");
            return new PostmanApiResult 
            { 
                IsSuccess = false, 
                ErrorMessage = "Request timeout",
                ErrorCode = "005"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred while calling Postman API");
            return new PostmanApiResult 
            { 
                IsSuccess = false, 
                ErrorMessage = ex.Message,
                ErrorCode = "005"
            };
        }
    }

    /// <summary>
    /// Convert Postman API errors to SMPP delivery status
    /// </summary>
    private static DeliveryStatusMapping GetDeliverSmStatusForApiError(PostmanErrorResponse? errorResponse)
    {
        if (errorResponse?.Code == null)
        {
            return new DeliveryStatusMapping
            {
                MessageState = "UNDELIVERABLE",
                ErrorStatus = "UNDELIV",
                ErrorCode = "005"
            };
        }

        // The errorCode mappings were decided by CPF 
        return errorResponse.Code switch
        {
            "parameter_invalid" => new DeliveryStatusMapping
            {
                MessageState = "UNDELIVERABLE",
                ErrorStatus = "UNDELIV", // Defined by Adobe experience
                ErrorCode = "001"
            },
            "invalid_ip_address_used" => new DeliveryStatusMapping
            {
                MessageState = "UNDELIVERABLE",
                ErrorStatus = "UNDELIV", // Defined by Adobe experience
                ErrorCode = "001"
            },
            "authentication_required" => new DeliveryStatusMapping
            {
                MessageState = "UNDELIVERABLE",
                ErrorStatus = "UNDELIV", // Defined by Adobe experience
                ErrorCode = "002"
            },
            "invalid_api_key_provided" => new DeliveryStatusMapping
            {
                MessageState = "UNDELIVERABLE",
                ErrorStatus = "UNDELIV", // Defined by Adobe experience
                ErrorCode = "002"
            },
            "invalid_path" => new DeliveryStatusMapping
            {
                MessageState = "UNDELIVERABLE",
                ErrorStatus = "UNDELIV", // Defined by Adobe experience
                ErrorCode = "003"
            },
            "too_many_requests" => new DeliveryStatusMapping
            {
                MessageState = "REJECTED",
                ErrorStatus = "REJECTED", // Defined by Adobe experience
                ErrorCode = "004"
            },
            
            _ => new DeliveryStatusMapping
            {
                MessageState = "UNDELIVERABLE",
                ErrorStatus = "UNDELIV", // Defined by Adobe experience
                ErrorCode = "005" // This represents a server error, we use it as the default
            }
        };
    }
}