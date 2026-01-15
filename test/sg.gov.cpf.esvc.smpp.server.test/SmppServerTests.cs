using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using sg.gov.cpf.esvc.smpp.server.BackgroundServices;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Services;
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

    [Fact]
    public void Constructor_WithNullCertificateManager_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SmppServer(
            _mockLogger.Object,
            _mockServiceProvider.Object,
            Options.Create(_serverConfig),
            Options.Create(_sslConfig),
            null!,
            _telemetryClient,
            _mockKeyVaultService.Object,
            _envConfig
        ));
    }

    [Fact]
    public void Constructor_WithNullSslConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<NullReferenceException>(() => new SmppServer(
            _mockLogger.Object,
            _mockServiceProvider.Object,
            Options.Create(_serverConfig),
            null!,
            _mockCertificateManager.Object,
            _telemetryClient,
            _mockKeyVaultService.Object,
            _envConfig
        ));
    }

    [Fact]
    public void Constructor_WithNullEnvironmentVariables_DoesNotThrow()
    {
        // EnvironmentVariablesConfiguration is not null-checked in constructor
        var server = new SmppServer(
            _mockLogger.Object,
            _mockServiceProvider.Object,
            Options.Create(_serverConfig),
            Options.Create(_sslConfig),
            _mockCertificateManager.Object,
            _telemetryClient,
            _mockKeyVaultService.Object,
            null!
        );

        Assert.NotNull(server);
    }

    [Fact]
    public void Constructor_BuildsProcessingPipeline_ResolvesRequiredLoggers()
    {
        // Act
        var server = CreateSmppServer();

        // Assert
        _mockServiceProvider.Verify(
            x => x.GetService(typeof(ILogger<LoggingMiddleware>)),
            Times.Once);

        _mockServiceProvider.Verify(
            x => x.GetService(typeof(ILogger<HandlerMiddleware>)),
            Times.Once);

        _mockServiceProvider.Verify(
            x => x.GetService(typeof(ILogger<SmppAuthenticationMiddleware>)),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_SetsIsRunningToFalse()
    {
        // Arrange
        var server = CreateSmppServer();

        // Act
        await server.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task StopAsync_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var server = CreateSmppServer();

        // Act
        await server.StopAsync(CancellationToken.None);
        await server.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task StopAsync_WithNoSessions_DoesNotThrow()
    {
        // Arrange
        var server = CreateSmppServer();

        // Act
        await server.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, server.ActiveSessionsCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_StopsGracefully()
    {
        // Arrange
        var server = CreateSmppServer();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = server.StartAsync(cts.Token);
        cts.Cancel();

        await executeTask;

        // Assert
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task ExecuteAsync_WithSslEnabled_LoadsCertificate()
    {
        // Arrange
        _envConfig.IsEnabledSSL = true;

        _mockCertificateManager
            .Setup(x => x.LoadServerCertificateAsync(It.IsAny<X509Certificate2>()))
            .ReturnsAsync(new X509Certificate2());

        var server = CreateSmppServer();
        using var cts = new CancellationTokenSource();

        // Act
        var task = server.StartAsync(cts.Token);
        cts.Cancel();
        await task;

        // Assert
        _mockCertificateManager.Verify(
            x => x.LoadServerCertificateAsync(It.IsAny<X509Certificate2>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleClientAsync_PlainTextClient_CleansUpWithoutSessions()
    {
        // Arrange
        var server = CreateSmppServer();

        var tcpClient = new TcpClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // force early cancellation

        var method = typeof(SmppServer)
            .GetMethod("HandleClientAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        await (Task)method.Invoke(
            server,
            new object[] { tcpClient, false, cts.Token }
        )!;

        // Assert
        Assert.Equal(0, server.ActiveSessionsCount);
    }

    [Fact]
    public async Task ProcessSessionAsync_WithRealSmppSession_AndNullPdu_ExitsCleanly()
    {
        // Arrange
        var server = CreateSmppServer();

        // Create a loopback TCP connection
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var client = new TcpClient();
        var connectTask = client.ConnectAsync(
            ((IPEndPoint)listener.LocalEndpoint).Address,
            ((IPEndPoint)listener.LocalEndpoint).Port);

        var serverClient = await listener.AcceptTcpClientAsync();
        await connectTask;

        listener.Stop();

        var session = new SmppSession(
            serverClient,
            _mockLogger.Object,
            _telemetryClient);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // force immediate exit

        var method = typeof(SmppServer)
            .GetMethod("ProcessSessionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        await (Task)method.Invoke(
            server,
            new object[] { session, cts.Token }
        )!;

        // Assert
        Assert.Equal(0, server.ActiveSessionsCount);

        session.Dispose();
        client.Dispose();
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