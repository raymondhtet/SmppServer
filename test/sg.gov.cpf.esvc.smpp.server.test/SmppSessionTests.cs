using System.Net;
using System.Net.Sockets;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class SmppSessionTests : IDisposable
{
    private readonly Mock<ILogger<SmppSession>> _mockLogger;
    private readonly TelemetryClient _telemetryClient;
    private TcpListener? _listener;
    private TcpClient? _client;

    public SmppSessionTests()
    {
        _mockLogger = new Mock<ILogger<SmppSession>>();
        
        var telemetryConfig = new TelemetryConfiguration();
        _telemetryClient = new TelemetryClient(telemetryConfig);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange
        var client = CreateTcpClient();

        // Act
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Assert
        Assert.NotNull(session);
        Assert.NotNull(session.Id);
        Assert.False(session.IsAuthenticated);
        Assert.True(session.IsConnected);
    }

    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SmppSession(null!, _mockLogger.Object, _telemetryClient));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateTcpClient();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SmppSession(client, null!, _telemetryClient));
    }

    [Fact]
    public void Id_AfterConstruction_IsNotEmpty()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act & Assert
        Assert.NotNull(session.Id);
        Assert.NotEmpty(session.Id);
        Assert.Equal(8, session.Id.Length); // Should be 8 chars
    }

    [Fact]
    public void SystemId_InitialState_IsNull()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act & Assert
        Assert.Null(session.SystemId);
    }

    [Fact]
    public void SystemId_CanBeSet()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);
        var systemId = "test-system";

        // Act
        session.SystemId = systemId;

        // Assert
        Assert.Equal(systemId, session.SystemId);
    }

    [Fact]
    public void IsAuthenticated_InitialState_IsFalse()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act & Assert
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_CanBeSet()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act
        session.IsAuthenticated = true;

        // Assert
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public void IsConnected_WithConnectedClient_ReturnsTrue()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act & Assert
        Assert.True(session.IsConnected);
    }

    [Fact]
    public void Pause_SetsSessionToPaused()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act
        session.Pause();

        // Assert - Session should be paused (ReadPduAsync should return null)
        var task = session.ReadPduAsync();
        Assert.Null(task.Result);
    }

    [Fact]
    public void Resume_AllowsSessionToResume()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);
        session.Pause();

        // Act
        session.Resume();

        // Assert - Session should be resumed
        Assert.True(session.IsConnected);
    }

    [Fact]
    public void Close_ClosesTheConnection()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act
        session.Close();

        // Assert
        Assert.False(session.IsConnected);
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act
        session.Dispose();

        // Assert
        Assert.False(session.IsConnected);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SmppSession(client, _mockLogger.Object, _telemetryClient);

        // Act & Assert
        session.Dispose();
        session.Dispose(); // Should not throw
    }

    private TcpClient CreateTcpClient()
    {
        // Create a listener to accept connection
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;

        // Create and connect client
        _client = new TcpClient();
        _client.Connect(endpoint);

        // Accept the connection
        var serverClient = _listener.AcceptTcpClient();
        
        return serverClient;
    }

    public void Dispose()
    {
        _client?.Close();
        _listener?.Stop();
    }
}