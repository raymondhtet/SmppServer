using Azure.Security.KeyVault.Secrets;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class AzureKeyVaultServiceTests
{
    private readonly Mock<ILogger<AzureKeyVaultService>> _mockLogger;
    private readonly Mock<SecretClient> _mockSecretClient;
    private readonly TelemetryClient _telemetryClient;
    private readonly EnvironmentVariablesConfiguration _envConfig;

    public AzureKeyVaultServiceTests()
    {
        _mockLogger = new Mock<ILogger<AzureKeyVaultService>>();
        _mockSecretClient = new Mock<SecretClient>();
        
        var telemetryConfig = new TelemetryConfiguration();
        _telemetryClient = new TelemetryClient(telemetryConfig);

        _envConfig = new EnvironmentVariablesConfiguration
        {
            SessionPasswordKey = "test-password-key",
            CampaignApiKeyMappingName = "test-mapping",
            IsEnabledSSL = false
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange
        var mockResponse = new Mock<Azure.Response<KeyVaultSecret>>();
        var secret = SecretModelFactory.KeyVaultSecret(
            new SecretProperties("test-secret"),
            "[]"
        );
        mockResponse.Setup(r => r.Value).Returns(secret);
        
        _mockSecretClient
            .Setup(x => x.GetSecret(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(mockResponse.Object);

        // Act
        var service = new AzureKeyVaultService(
            _mockLogger.Object,
            _mockSecretClient.Object,
            _telemetryClient,
            _envConfig
        );

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.SessionPassword);
    }

    [Fact]
    public void GetSecret_WithValidSecretName_ReturnsSecretValue()
    {
        // Arrange
        var secretName = "test-secret";
        var secretValue = "test-value";
        
        var mockResponse = new Mock<Azure.Response<KeyVaultSecret>>();
        var secret = SecretModelFactory.KeyVaultSecret(
            new SecretProperties(secretName),
            secretValue
        );
        mockResponse.Setup(r => r.Value).Returns(secret);
        
        _mockSecretClient
            .Setup(x => x.GetSecret(secretName, null, default))
            .Returns(mockResponse.Object);

        var service = CreateService();

        // Act
        var result = service.GetSecret(secretName);

        // Assert
        Assert.Equal(secretValue, result);
    }

    [Fact]
    public void SessionPassword_AfterInitialization_ReturnsValue()
    {
        // Arrange
        var password = "test-password";
        SetupSecretClientMock(_envConfig.SessionPasswordKey, password);
        
        var service = CreateService();

        // Act
        var result = service.SessionPassword;

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RefreshCacheValues_CallsLoadConfigurationValues()
    {
        // Arrange
        SetupSecretClientMock(_envConfig.SessionPasswordKey, "password");
        var service = CreateService();

        // Act
        service.RefreshCacheValues();

        // Assert
        _mockSecretClient.Verify(
            x => x.GetSecret(It.IsAny<string>(), null, default),
            Times.AtLeastOnce
        );
    }

    private AzureKeyVaultService CreateService()
    {
        SetupSecretClientMock(_envConfig.SessionPasswordKey, "test-password");
        SetupSecretClientMock(_envConfig.CampaignApiKeyMappingName, 
            "[{\"campaignId\":\"test-campaign\",\"apiKey\":\"test-key\",\"scheme\":\"bearer\"}]");

        return new AzureKeyVaultService(
            _mockLogger.Object,
            _mockSecretClient.Object,
            _telemetryClient,
            _envConfig
        );
    }

    private void SetupSecretClientMock(string secretName, string secretValue)
    {
        var mockResponse = new Mock<Azure.Response<KeyVaultSecret>>();
        var secret = SecretModelFactory.KeyVaultSecret(
            new SecretProperties(secretName),
            secretValue
        );
        mockResponse.Setup(r => r.Value).Returns(secret);
        
        _mockSecretClient
            .Setup(x => x.GetSecret(secretName, null, default))
            .Returns(mockResponse.Object);
    }
}