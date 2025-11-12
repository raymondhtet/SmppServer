using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Options;
using sg.gov.cpf.esvc.sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Exceptions;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;
using System.Text;
using System.Text.Json;

namespace sg.gov.cpf.esvc.smpp.server.Services;

public class PostmanApiService(
    HttpClient httpClient,
    ILogger<PostmanApiService> logger,
    TelemetryClient telemetryClient,
    IOptions<PostmanApiConfiguration> configuration,
    IOptions<WhitelistedSmsConfiguration> whitelistedSmsConfiguration,
    IKeyVaultService keyVaultService,
    EnvironmentVariablesConfiguration environmentVariables
    )
    : IExternalMessageService
{
    private readonly PostmanApiConfiguration _apiConfiguration = configuration.Value;
    private readonly WhitelistedSmsConfiguration _whitelistedSmsConfiguration = whitelistedSmsConfiguration.Value;

    public async Task<ExternalServiceResult> SendMessageAsync(
        string systemId,
        string recipientMobileNumber,
        string message,
        string campaignId,
        string messageId,
        int? delayInSeconds,
        CancellationToken cancellationToken)
    {
        using var _ = telemetryClient.StartOperation<DependencyTelemetry>(nameof(PostmanApiService));
        try
        {
            telemetryClient.Context.Operation.Id = messageId;
            logger.LogInformation("Sending message via Postman API: {SystemId} -> 65****{Destination}: '{Message}' and Campaign Id: {CampaignId}",
                systemId, recipientMobileNumber.Substring(6), message, campaignId);

            telemetryClient.TrackTrace($"Is Whitelisted SMS Configuration enabled? {environmentVariables.IsWhitelistedEnabled}");

            if (environmentVariables.IsWhitelistedEnabled)
            {
                if (delayInSeconds != null)
                {
                    logger.LogInformation("Simulating delay from postman API and delay for mobile number {MobileNumber} is {Delay} Seconds",
                            recipientMobileNumber, delayInSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delayInSeconds.Value), cancellationToken);
                    return new ExternalServiceResult(true, ID: messageId);
                }
                else
                {
                    var whitelistedNumber = _whitelistedSmsConfiguration.WhitelistedMobileNumbers.FirstOrDefault(w => w.MobileNumber == recipientMobileNumber);
                    if (whitelistedNumber != null)
                    {
                        logger.LogInformation("Whitelisted number flag for sending sms: {IsSentSMS}", whitelistedNumber.IsSentSMS);
                        if (whitelistedNumber.IsSentSMS)
                        {
                            logger.LogInformation("Sending actual sms through postman");
                            return await TriggerPostmanApi(message, messageId, recipientMobileNumber, campaignId, cancellationToken);
                        }
                    }
                }

                telemetryClient.TrackTrace($"The recipient mobile number is not whitelisted yet ({recipientMobileNumber})");
            }
            else if (environmentVariables.IsProduction)
            {
                telemetryClient.TrackTrace("Sending SMS in production");
                return await TriggerPostmanApi(message, messageId, recipientMobileNumber, campaignId, cancellationToken);
            }
            else
            {
                logger.LogError("Whitelisting is not enabled in the non-prod");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while sending message via Postman API");
            return new ExternalServiceResult(IsSuccess: false, ErrorMessage: ex.Message, ErrorCode: "API_ERROR", null);
        }

        return new ExternalServiceResult(IsSuccess: false, ErrorMessage: string.Empty, ErrorCode: "API_ERROR", null); 
    }

    public async Task<ExternalServiceResult> TriggerPostmanApi(string message, string messageId, string recipientMobileNumber, string campaignIdFromPdu, CancellationToken cancellationToken)
    {
        // Get configuration values
        var postmanBaseUrl = environmentVariables.PostmanBaseUrl;
        if (string.IsNullOrEmpty(postmanBaseUrl))
        {
            logger.LogError("PostmanBaseUrl configuration is missing");
            return new ExternalServiceResult(IsSuccess: false, ErrorMessage: "Configuration error", ErrorCode: "CONFIG_ERROR", null);
        }

        // Get configuration values
        var singleSmsUrl = _apiConfiguration.SingleSmsUrl;
        if (string.IsNullOrEmpty(singleSmsUrl))
        {
            logger.LogError("singleSmsUrl configuration is missing");
            return new ExternalServiceResult(IsSuccess: false, ErrorMessage: "Configuration error", ErrorCode: "CONFIG_ERROR", null);
        }

        if (string.IsNullOrEmpty(message))
        {
            logger.LogError("message is empty");
            return new ExternalServiceResult(IsSuccess: false, ErrorMessage: "message body is empty", ErrorCode: "001", null);
        }


        (string campaignId, string apiKey) = GetConfigurationSettingsByCampaignId(campaignIdFromPdu);


        // build postman url
        var postmanFullUrl = $"{postmanBaseUrl}/{string.Format(singleSmsUrl, campaignId)}";

        var result = await SendMessageToPostmanApi(apiKey, recipientMobileNumber, message, "english", postmanFullUrl, cancellationToken);

        if (result.IsSuccess)
        {
            telemetryClient.TrackTrace($"Message sent successfully via Postman API for Message ID:{messageId} Postman ID:{result.ID} campaign Id:{campaignId} and mobile number:65****{recipientMobileNumber.Substring(6)}");
            return new ExternalServiceResult(IsSuccess: true, ID: result.ID);
        }
        else
        {
            telemetryClient.TrackException(new PostmanException(
                result.ErrorMessage,
                result.ErrorCode,
                result.ErrorCode,
                campaignId,
                recipientMobileNumber.Substring(6),
                result.ID,
                messageId,
                result.ErrorStackTrace));
            return new ExternalServiceResult(IsSuccess: false, ErrorMessage: result.ErrorMessage, ErrorCode: result.ErrorCode, ID: result.ID);
        }
    }

    private (string campaignId, string apiKey) GetConfigurationSettingsByCampaignId(string campaignIdFromPdu)
    {
        if (string.IsNullOrEmpty(campaignIdFromPdu))
            return (keyVaultService.PostmanCampaignApiKeyMappings![0].CampaignId, keyVaultService.PostmanCampaignApiKeyMappings[0].ApiKey);

        var mapping = keyVaultService.PostmanCampaignApiKeyMappings!.Where(p => p.CampaignId.Trim() == campaignIdFromPdu.Trim()).FirstOrDefault();

        return mapping == null
            ? throw new SmppConfigurationException("CampaignId", $"The campaign Id {campaignIdFromPdu} does not exist in the mapping")
            : (mapping.CampaignId, mapping.ApiKey);
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

        string responseContent = string.Empty;
        HttpResponseMessage? response = null;

        try
        {
            // 1. Create headers for request
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey.Trim()}");

            // 2. Create request body
            var requestBody = new
            {
                recipient = recipient.Trim(),
                language = language,
                values = new
                {
                    body = messageBody.Trim()
                }
            };

            var jsonBody = JsonSerializer.Serialize(requestBody, GetJsonSerializerOptions());

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            response = await httpClient.PostAsync(postmanFullUrl, content, cancellationToken);
            responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            PostmanResponse? postmanResponse = JsonSerializer.Deserialize<PostmanResponse>(responseContent, GetJsonSerializerOptions());

            if (response.IsSuccessStatusCode)
            {
                return new PostmanApiResult { IsSuccess = true, ID = postmanResponse?.ID };
            }
            else
            {
                string? responseID = string.IsNullOrWhiteSpace(postmanResponse?.ID) ? postmanResponse?.Error?.ID : postmanResponse?.ID;
                var errorMapping = GetDeliverSmStatusForApiError(postmanResponse);

                logger.LogInformation("Postman API returned error details: {ErrorResponse}, {ApiKey}, Request Body JSON: {ErrorJson}",
                    responseContent, apiKey.Trim(), jsonBody);

                telemetryClient.TrackTrace($"Postman API returned error code:{postmanResponse?.Error?.Code}, " +
                    $"Message:{postmanResponse?.Error?.Message}, " +
                    $"Type:{postmanResponse?.Error?.Type}, " +
                    $"ID:{responseID}");

                return new PostmanApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = postmanResponse?.Error?.Message ?? "Unknown error",
                    ErrorCode = errorMapping.ErrorCode,
                    MessageState = errorMapping.MessageState,
                    ErrorStatus = errorMapping.ErrorStatus,
                    ID = responseID,
                };

            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error occurred while calling Postman API");
            return new PostmanApiResult
            {
                IsSuccess = false,
                ErrorMessage = "HTTP error occurred while calling Postman API:" + ex.Message,
                ErrorCode = "005",
                ErrorStackTrace = ex.StackTrace
            };
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Postman API request timed out");
            return new PostmanApiResult
            {
                IsSuccess = false,
                ErrorMessage = "Postman API request timed out" + ex.Message,
                ErrorCode = "005",
                ErrorStackTrace = ex.StackTrace
            };
        }
        catch (JsonException ex)
        {
            logger.LogError("Failed to parse Postman API error response: {ResponseContent}", responseContent);
            return new PostmanApiResult
            {
                IsSuccess = false,
                ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}:" + ex.Message,
                ErrorCode = "005",
                ErrorStackTrace = ex.StackTrace
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred while calling Postman API");
            return new PostmanApiResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ErrorStackTrace = ex.StackTrace,
                ErrorCode = "005"
            };
        }
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Convert Postman API errors to SMPP delivery status
    /// </summary>
    private static DeliveryStatusMapping GetDeliverSmStatusForApiError(PostmanResponse? errorResponse)
    {
        if (errorResponse?.Error?.Code == null)
        {
            return new DeliveryStatusMapping
            {
                MessageState = "UNDELIVERABLE",
                ErrorStatus = "UNDELIV",
                ErrorCode = "005"
            };
        }

        // The errorCode mappings were decided by CPF 
        return errorResponse.Error?.Code switch
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