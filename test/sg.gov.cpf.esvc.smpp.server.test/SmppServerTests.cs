using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using sg.gov.cpf.esvc.smpp.server.BackgroundServices;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using Ssg.gov.cpf.esvc.smpp.server.Middlewares;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class SmppServerTests
{
    private readonly Mock<ILogger<SmppServer>> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IKeyVaultService> _mockKeyVaultService;
    private readonly Mock<ISslCertificateManager> _mockCertificateManager;
    private readonly SmppServerConfiguration _serverConfig;
    private readonly SslConfiguration _sslConfig;
    private readonly EnvironmentVariablesConfiguration _envConfig;
    private readonly TelemetryClient _telemetryClient;

    public SmppServerTests()
    {
        _mockLogger = new Mock<ILogger<SmppServer>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockKeyVaultService = new Mock<IKeyVaultService>();
        _mockCertificateManager = new Mock<ISslCertificateManager>();

        _serverConfig = new SmppServerConfiguration
        {
            Port = 2775,
            MaxConcurrentConnections = 100,
            CleanUpJobInterval = "00:01:00",
            StaleCleanUpInterval = "00:02:00"
        };

        _sslConfig = new SslConfiguration
        {
            Port = 2776
        };

        _envConfig = new EnvironmentVariablesConfiguration
        {
            IsEnabledSSL = false,
            SessionUserName = "testuser"
        };

        var telemetryConfig = new TelemetryConfiguration();
        _telemetryClient = new TelemetryClient(telemetryConfig);

        // Setup service provider to return mock loggers for middleware
        SetupServiceProvider();
    }

    private void SetupServiceProvider()
    {
        // Mock loggers for middleware components
        var mockLoggingMiddlewareLogger = new Mock<ILogger<LoggingMiddleware>>();
        var mockHandlerMiddlewareLogger = new Mock<ILogger<HandlerMiddleware>>();
        var mockAuthMiddlewareLogger = new Mock<ILogger<SmppAuthenticationMiddleware>>();

        // Setup service provider to return these loggers
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(ILogger<LoggingMiddleware>)))
            .Returns(mockLoggingMiddlewareLogger.Object);
        
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(ILogger<HandlerMiddleware>)))
            .Returns(mockHandlerMiddlewareLogger.Object);
        
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(ILogger<SmppAuthenticationMiddleware>)))
            .Returns(mockAuthMiddlewareLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SmppServer(
            null!,
            _mockServiceProvider.Object,
            Options.Create(_serverConfig),
            Options.Create(_sslConfig),
            _mockCertificateManager.Object,
            _telemetryClient,
            _mockKeyVaultService.Object,
            _envConfig
        ));
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SmppServer(
            _mockLogger.Object,
            null!,
            Options.Create(_serverConfig),
            Options.Create(_sslConfig),
            _mockCertificateManager.Object,
            _telemetryClient,
            _mockKeyVaultService.Object,
            _envConfig
        ));
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange & Act
        var server = CreateSmppServer();

        // Assert
        Assert.NotNull(server);
        Assert.False(server.IsRunning);
        Assert.Equal(0, server.ActiveSessionsCount);
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<NullReferenceException>(() => new SmppServer(
            _mockLogger.Object,
            _mockServiceProvider.Object,
            null!,
            Options.Create(_sslConfig),
            _mockCertificateManager.Object,
            _telemetryClient,
            _mockKeyVaultService.Object,
            _envConfig
        ));
    }

    [Fact]
    public void IsRunning_InitialState_ReturnsFalse()
    {
        // Arrange
        var server = CreateSmppServer();

        // Act & Assert
        Assert.False(server.IsRunning);
    }

    [Fact]
    public void ActiveSessionsCount_InitialState_ReturnsZero()
    {
        // Arrange
        var server = CreateSmppServer();

        // Act & Assert
        Assert.Equal(0, server.ActiveSessionsCount);
    }

    private SmppServer CreateSmppServer()
    {
        return new SmppServer(
            _mockLogger.Object,
            _mockServiceProvider.Object,
            Options.Create(_serverConfig),
            Options.Create(_sslConfig),
            _mockCertificateManager.Object,
            _telemetryClient,
            _mockKeyVaultService.Object,
            _envConfig
        );
    }
}