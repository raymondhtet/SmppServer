using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class ConfigurationAuthenticationServiceTests
{
    private readonly Mock<ILogger<ConfigurationAuthenticationService>> _mockLogger;
    private readonly Mock<IKeyVaultService> _mockKeyVaultService;
    private readonly SmppServerConfiguration _serverConfig;
    private readonly EnvironmentVariablesConfiguration _envConfig;

    public ConfigurationAuthenticationServiceTests()
    {
        _mockLogger = new Mock<ILogger<ConfigurationAuthenticationService>>();
        _mockKeyVaultService = new Mock<IKeyVaultService>();
        
        _serverConfig = new SmppServerConfiguration
        {
            SystemId = "test-system"
        };

        _envConfig = new EnvironmentVariablesConfiguration
        {
            SessionUserName = "testuser"
        };

        _mockKeyVaultService
            .Setup(x => x.SessionPassword)
            .Returns("testpassword");
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var systemId = "testuser";
        var password = "testpassword";

        // Act
        var result = await service.AuthenticateAsync(systemId, password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidSystemId_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var systemId = "wronguser";
        var password = "testpassword";

        // Act
        var result = await service.AuthenticateAsync(systemId, password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidPassword_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var systemId = "testuser";
        var password = "wrongpassword";

        // Act
        var result = await service.AuthenticateAsync(systemId, password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptySystemId_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var systemId = "";
        var password = "testpassword";

        // Act
        var result = await service.AuthenticateAsync(systemId, password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var systemId = "testuser";
        var password = "";

        // Act
        var result = await service.AuthenticateAsync(systemId, password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AuthenticateAsync_LogsAuthenticationAttempt()
    {
        // Arrange
        var service = CreateService();
        var systemId = "testuser";
        var password = "testpassword";

        // Act
        await service.AuthenticateAsync(systemId, password);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Authentication attempt")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private ConfigurationAuthenticationService CreateService()
    {
        return new ConfigurationAuthenticationService(
            Options.Create(_serverConfig),
            _mockLogger.Object,
            _mockKeyVaultService.Object,
            _envConfig
        );
    }
}