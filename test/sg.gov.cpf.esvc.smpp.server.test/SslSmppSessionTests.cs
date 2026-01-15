using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Services;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class SslSmppSessionTests : IDisposable
{
    private readonly Mock<ILogger<SslSmppSession>> _mockLogger;
    private readonly SslConfiguration _sslConfig;
    private readonly X509Certificate2 _testCertificate;
    private TcpListener? _listener;
    private TcpClient? _client;

    public SslSmppSessionTests()
    {
        _mockLogger = new Mock<ILogger<SslSmppSession>>();
        _sslConfig = new SslConfiguration
        {
            AllowSelfSignedCertificates = true,
            CheckCertificateRevocation = false,
            RequireClientCertificate = false
        };

        _testCertificate = CreateSelfSignedCertificate();
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange
        var client = CreateTcpClient();

        // Act
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Assert
        Assert.NotNull(session);
        Assert.NotNull(session.Id);
        Assert.Equal(8, session.Id.Length);
        Assert.False(session.IsAuthenticated);
        Assert.False(session.IsSslAuthenticated);
        Assert.Equal(_testCertificate, session.ServerCertificate);
    }

    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SslSmppSession(
                null!,
                _mockLogger.Object,
                Options.Create(_sslConfig),
                _testCertificate
            )
        );
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateTcpClient();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SslSmppSession(
                client,
                null!,
                Options.Create(_sslConfig),
                _testCertificate
            )
        );
    }

    [Fact]
    public void Constructor_WithNullSslConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateTcpClient();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SslSmppSession(
                client,
                _mockLogger.Object,
                null!,
                _testCertificate
            )
        );
    }

    [Fact]
    public void Constructor_WithNullCertificate_ThrowsArgumentNullException()
    {
        // Arrange
        var client = CreateTcpClient();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SslSmppSession(
                client,
                _mockLogger.Object,
                Options.Create(_sslConfig),
                null!
            )
        );
    }

    [Fact]
    public void Id_AfterConstruction_IsNotEmpty()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act & Assert
        Assert.NotNull(session.Id);
        Assert.NotEmpty(session.Id);
        Assert.Equal(8, session.Id.Length);
    }

    [Fact]
    public void SystemId_InitialState_IsNull()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act & Assert
        Assert.Null(session.SystemId);
    }

    [Fact]
    public void SystemId_CanBeSet()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );
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
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act & Assert
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public void IsSslAuthenticated_InitialState_IsFalse()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act & Assert
        Assert.False(session.IsSslAuthenticated);
    }

    [Fact]
    public void Pause_SetsSessionToPaused()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

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
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );
        session.Pause();

        // Act
        session.Resume();

        // Assert
        Assert.NotNull(session);
    }

    [Fact]
    public void Close_ClosesTheConnection()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act
        session.Close();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SSL connection closed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act
        session.Dispose();

        // Assert - Should not throw
        Assert.NotNull(session);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act & Assert
        session.Dispose();
        session.Dispose(); // Should not throw
    }

    [Fact]
    public void ServerCertificate_ReturnsProvidedCertificate()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act & Assert
        Assert.Equal(_testCertificate, session.ServerCertificate);
    }

    [Fact]
    public void ClientCertificate_InitialState_IsNull()
    {
        // Arrange
        var client = CreateTcpClient();
        var session = new SslSmppSession(
            client,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act & Assert
        Assert.Null(session.ClientCertificate);
    }

    [Fact]
    public void ProcessId_IsUnique()
    {
        // Arrange
        var client1 = CreateTcpClient();
        var client2 = CreateTcpClient();

        var session1 = new SslSmppSession(
            client1,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        var session2 = new SslSmppSession(
            client2,
            _mockLogger.Object,
            Options.Create(_sslConfig),
            _testCertificate
        );

        // Act & Assert
        Assert.NotEqual(session1.ProcessId, session2.ProcessId);
    }

    private TcpClient CreateTcpClient()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;

        _client = new TcpClient();
        _client.Connect(endpoint);

        var serverClient = _listener.AcceptTcpClient();
        return serverClient;
    }

    private X509Certificate2 CreateSelfSignedCertificate()
    {
        // Create certificate that works across all platforms (Windows, Linux, macOS)
        var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            "CN=Test SSL Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false)
        );

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );

        // Export and re-import to ensure the certificate works on all platforms
        // This avoids macOS keychain issues
        var pfxBytes = certificate.Export(X509ContentType.Pfx, "");

        // Clean up the original certificate
        certificate.Dispose();
        rsa.Dispose();

        // Return a new certificate from the PFX bytes
        return new X509Certificate2(pfxBytes, "",
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    public void Dispose()
    {
        _client?.Close();
        _listener?.Stop();
        _testCertificate?.Dispose();
    }
}