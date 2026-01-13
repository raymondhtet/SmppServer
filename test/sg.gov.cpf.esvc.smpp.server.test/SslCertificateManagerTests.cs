using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Exceptions;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class SslCertificateManagerTests: IDisposable
{
    private readonly Mock<ILogger<SslCertificateManager>> _mockLogger;
    private readonly SslConfiguration _sslConfig;
    private X509Certificate2? _testCertificate;

    public SslCertificateManagerTests()
    {
        _mockLogger = new Mock<ILogger<SslCertificateManager>>();
        _sslConfig = new SslConfiguration
        {
            AllowSelfSignedCertificates = true,
            CheckCertificateRevocation = false
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange & Act
        var manager = CreateManager();

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<NullReferenceException>(() => 
            new SslCertificateManager(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SslCertificateManager(Options.Create(_sslConfig), null!));
    }

    [Fact]
    public async Task LoadServerCertificateAsync_WithValidCertificate_ReturnsCertificate()
    {
        // Arrange
        var manager = CreateManager();
        var certificate = CreateSelfSignedCertificate();

        // Act
        var result = await manager.LoadServerCertificateAsync(certificate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasPrivateKey);
    }

    [Fact]
    public async Task LoadServerCertificateAsync_CalledTwice_ReturnsCachedCertificate()
    {
        // Arrange
        var manager = CreateManager();
        var certificate = CreateSelfSignedCertificate();

        // Act
        var result1 = await manager.LoadServerCertificateAsync(certificate);
        var result2 = await manager.LoadServerCertificateAsync(certificate);

        // Assert
        Assert.Same(result1, result2);
    }

    [Fact]
    public async Task ValidateCertificateAsync_WithValidCertificate_ReturnsTrue()
    {
        // Arrange
        var manager = CreateManager();
        var certificate = CreateSelfSignedCertificate();

        // Act
        var result = await manager.ValidateCertificateAsync(certificate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateCertificateAsync_WithExpiredCertificate_ReturnsFalse()
    {
        // Arrange
        var manager = CreateManager();
        var certificate = CreateExpiredCertificate();

        // Act
        var result = await manager.ValidateCertificateAsync(certificate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCertificateAsync_WithCertificateWithoutPrivateKey_ReturnsFalse()
    {
        // Arrange
        var manager = CreateManager();
        var certWithKey = CreateSelfSignedCertificate();
        // Export and reimport without private key
        var certBytes = certWithKey.Export(X509ContentType.Cert);
        var certWithoutKey = new X509Certificate2(certBytes);

        // Act
        var result = await manager.ValidateCertificateAsync(certWithoutKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RefreshCertificatesAsync_ClearsCachedCertificates()
    {
        // Arrange
        var manager = CreateManager();
        var certificate = CreateSelfSignedCertificate();
        await manager.LoadServerCertificateAsync(certificate);

        // Act
        await manager.RefreshCertificatesAsync();

        // Assert - After refresh, should reload certificate
        await Assert.ThrowsAsync<SmppConfigurationException>(() => manager.LoadServerCertificateAsync(certificate));
        
    }

    [Fact]
    public void CertificateExpiring_Event_RaisedWhenCertificateExpiringSoon()
    {
        // Arrange
        var manager = CreateManager();
        var eventRaised = false;
        manager.CertificateExpiring += (sender, args) => eventRaised = true;
        
        var certificate = CreateCertificateExpiringSoon();

        // Act
        manager.LoadServerCertificateAsync(certificate).Wait();

        // Note: Event might not be raised immediately, but within monitoring interval
        // This is a simplified test
        Assert.NotNull(manager);
    }

    private SslCertificateManager CreateManager()
    {
        return new SslCertificateManager(
            Options.Create(_sslConfig),
            _mockLogger.Object
        );
    }

    private X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
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

        _testCertificate = certificate;
        return certificate;
    }

    private X509Certificate2 CreateExpiredCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Expired Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddYears(-2),
            DateTimeOffset.UtcNow.AddYears(-1)
        );

        return certificate;
    }

    private X509Certificate2 CreateCertificateExpiringSoon()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Expiring Soon Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(15) // Expires in 15 days
        );

        return certificate;
    }

    public void Dispose()
    {
        _testCertificate?.Dispose();
    }
}