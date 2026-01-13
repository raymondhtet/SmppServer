using System.Net;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using sg.gov.cpf.esvc.sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Exceptions;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class PostmanApiServiceTests
{
    private readonly Mock<ILogger<PostmanApiService>> _mockLogger;
    private readonly TelemetryClient _telemetryClient;
    private readonly Mock<IKeyVaultService> _mockKeyVaultService;
    private readonly PostmanApiConfiguration _apiConfig;
    private readonly WhitelistedSmsConfiguration _whitelistedConfig;
    private readonly EnvironmentVariablesConfiguration _envConfig;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;

    public PostmanApiServiceTests()
    {
        _mockLogger = new Mock<ILogger<PostmanApiService>>();
        
        var telemetryConfig = new TelemetryConfiguration();
        _telemetryClient = new TelemetryClient(telemetryConfig);
        
        _mockKeyVaultService = new Mock<IKeyVaultService>();
        
        _apiConfig = new PostmanApiConfiguration
        {
            SingleSmsUrl = "campaigns/{0}/messages"
        };
        
        _whitelistedConfig = new WhitelistedSmsConfiguration
        {
            WhitelistedMobileNumbers = new List<WhitelistedInfo>
            {
                new WhitelistedInfo { MobileNumber = "1234567890", IsSentSMS = true }
            }
        };
        
        _envConfig = new EnvironmentVariablesConfiguration
        {
            PostmanBaseUrl = "https://api.postman.test",
            IsWhitelistedEnabled = false
        };

        // Setup mock mappings
        _mockKeyVaultService
            .Setup(x => x.PostmanCampaignApiKeyMappings)
            .Returns(new List<PostmanCampaignApiKeyMapping>
            {
                new PostmanCampaignApiKeyMapping("campaign-123", "api-key-123", "Bearer")
            });

        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
    }

    [Fact]
    public async Task SendMessageAsync_InProduction_SendsToPostmanApi()
    {
        // Arrange
        _envConfig.Environment = "prd";
        var service = CreateService();
        SetupSuccessfulHttpResponse();

        // Act
        var result = await service.SendMessageAsync(
            "test-system",
            "1234567890",
            "Test message",
            "campaign-123",
            "msg-123",
            null,
            CancellationToken.None
        );

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SendMessageAsync_WithWhitelistedNumber_SendsMessage()
    {
        // Arrange
        _envConfig.IsWhitelistedEnabled = true;
        var service = CreateService();
        SetupSuccessfulHttpResponse();

        // Act
        var result = await service.SendMessageAsync(
            "test-system",
            "1234567890",
            "Test message",
            "campaign-123",
            "msg-123",
            null,
            CancellationToken.None
        );

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SendMessageAsync_WithNonWhitelistedNumber_ReturnsFailure()
    {
        // Arrange
        _envConfig.IsWhitelistedEnabled = true;
        var service = CreateService();

        // Act
        var result = await service.SendMessageAsync(
            "test-system",
            "9999999999",
            "Test message",
            "campaign-123",
            "msg-123",
            null,
            CancellationToken.None
        );

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task SendMessageAsync_WithDelay_SimulatesDelay()
    {
        // Arrange
        _envConfig.IsWhitelistedEnabled = true;
        var service = CreateService();
        var delaySeconds = 2;
        var startTime = DateTime.UtcNow;

        // Act
        var result = await service.SendMessageAsync(
            "test-system",
            "1234567890",
            "Test message",
            "campaign-123",
            "msg-123",
            delaySeconds,
            CancellationToken.None
        );

        // Assert
        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
        Assert.True(result.IsSuccess);
        Assert.True(elapsed >= delaySeconds);
    }

    [Fact]
    public async Task TriggerPostmanApi_WithEmptyMessage_ReturnsError()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.TriggerPostmanApi(
            "",
            "msg-123",
            "1234567890",
            "campaign-123",
            CancellationToken.None
        );

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("001", result.ErrorCode);
    }

    [Fact]
    public async Task TriggerPostmanApi_WithSuccessResponse_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService();
        SetupSuccessfulHttpResponse();

        // Act
        var result = await service.TriggerPostmanApi(
            "Test message",
            "msg-123",
            "1234567890",
            "campaign-123",
            CancellationToken.None
        );

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TriggerPostmanApi_WithErrorResponse_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        SetupErrorHttpResponse();

        // Act
        var result = await service.TriggerPostmanApi(
            "Test message",
            "msg-123",
            "1234567890",
            "campaign-123",
            CancellationToken.None
        );

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorCode);
    }

    [Fact]
    public async Task TriggerPostmanApi_WithInvalidCampaignId_ThrowsException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<SmppConfigurationException>(async () =>
            await service.TriggerPostmanApi(
                "Test message",
                "msg-123",
                "1234567890",
                "invalid-campaign",
                CancellationToken.None
            )
        );
    }

    private PostmanApiService CreateService()
    {
        return new PostmanApiService(
            _httpClient,
            _mockLogger.Object,
            _telemetryClient,
            Options.Create(_apiConfig),
            Options.Create(_whitelistedConfig),
            _mockKeyVaultService.Object,
            _envConfig
        );
    }

    private void SetupSuccessfulHttpResponse()
    {
        var responseContent = JsonSerializer.Serialize(new
        {
            id = "postman-msg-123",
            status = "sent"
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });
    }

    private void SetupErrorHttpResponse()
    {
        var responseContent = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = "parameter_invalid",
                message = "Invalid parameter",
                type = "validation_error",
                id = "error-123"
            }
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(responseContent)
            });
    }
}