using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using sg.gov.cpf.esvc.sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class ConfigurationTests
{
    [Fact]
    public void SmppServerConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new SmppServerConfiguration();

        // Assert
        Assert.Equal(2775, config.Port);
        Assert.Equal(1000, config.MaxConcurrentConnections);
        Assert.Equal("00:01:00", config.StaleCleanUpInterval);
        Assert.Equal("00:01:30", config.CleanUpJobInterval);
        Assert.Equal("01:00:00", config.CacheDuration);
        Assert.Equal("", config.SystemId);
    }

    [Fact]
    public void SmppServerConfiguration_CanSetProperties()
    {
        // Arrange
        var config = new SmppServerConfiguration();

        // Act
        config.Port = 3000;
        config.MaxConcurrentConnections = 500;
        config.SystemId = "test-system";

        // Assert
        Assert.Equal(3000, config.Port);
        Assert.Equal(500, config.MaxConcurrentConnections);
        Assert.Equal("test-system", config.SystemId);
    }

    [Fact]
    public void SslConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new SslConfiguration();

        // Assert
        Assert.Equal(2776, config.Port);
        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, config.SupportedProtocols);
        Assert.False(config.RequireClientCertificate);
        Assert.False(config.CheckCertificateRevocation);
        Assert.False(config.AllowSelfSignedCertificates);
        Assert.Empty(config.TrustedCACertificates);
        Assert.Equal(TimeSpan.FromSeconds(30), config.HandshakeTimeout);
    }

    [Fact]
    public void SslConfiguration_CanSetProperties()
    {
        // Arrange
        var config = new SslConfiguration();

        // Act
        config.Port = 3000;
        config.RequireClientCertificate = true;
        config.CheckCertificateRevocation = true;
        config.AllowSelfSignedCertificates = true;
        config.HandshakeTimeout = TimeSpan.FromSeconds(60);

        // Assert
        Assert.Equal(3000, config.Port);
        Assert.True(config.RequireClientCertificate);
        Assert.True(config.CheckCertificateRevocation);
        Assert.True(config.AllowSelfSignedCertificates);
        Assert.Equal(TimeSpan.FromSeconds(60), config.HandshakeTimeout);
    }

    [Fact]
    public void EnvironmentVariablesConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new EnvironmentVariablesConfiguration();

        // Assert
        Assert.Equal(string.Empty, config.KeyVaultUri);
        Assert.False(config.IsEnabledSSL);
        Assert.False(config.IsDeliverSmEnabled);
        Assert.Equal(string.Empty, config.SessionUserName);
        Assert.Equal(string.Empty, config.AppInsightConnectionString);
        Assert.False(config.IsWhitelistedEnabled);
        Assert.Equal(string.Empty, config.Environment);
    }

    [Fact]
    public void EnvironmentVariablesConfiguration_IsProduction_ReturnsTrueForPrd()
    {
        // Arrange
        var config = new EnvironmentVariablesConfiguration
        {
            Environment = "prd"
        };

        // Act & Assert
        Assert.True(config.IsProduction);
    }

    [Fact]
    public void EnvironmentVariablesConfiguration_IsProduction_ReturnsFalseForNonPrd()
    {
        // Arrange
        var config = new EnvironmentVariablesConfiguration
        {
            Environment = "dev"
        };

        // Act & Assert
        Assert.False(config.IsProduction);
    }

    [Fact]
    public void EnvironmentVariablesConfiguration_MinimumLogLevel_MapsCorrectly()
    {
        // Arrange & Act & Assert
        var config = new EnvironmentVariablesConfiguration();

        config.LogLevelString = "info";
        Assert.Equal(LogLevel.Information, config.MinimumLogLevel);

        config.LogLevelString = "debug";
        Assert.Equal(LogLevel.Debug, config.MinimumLogLevel);

        config.LogLevelString = "warn";
        Assert.Equal(LogLevel.Warning, config.MinimumLogLevel);

        config.LogLevelString = "error";
        Assert.Equal(LogLevel.Error, config.MinimumLogLevel);

        config.LogLevelString = "invalid";
        Assert.Equal(LogLevel.Error, config.MinimumLogLevel);
    }

    [Fact]
    public void WhitelistedSmsConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new WhitelistedSmsConfiguration();

        // Assert
        Assert.NotNull(config.WhitelistedMobileNumbers);
        Assert.Empty(config.WhitelistedMobileNumbers);
    }

    [Fact]
    public void WhitelistedInfo_CanSetProperties()
    {
        // Arrange
        var info = new WhitelistedInfo();

        // Act
        info.MobileNumber = "1234567890";
        info.IsSentSMS = true;

        // Assert
        Assert.Equal("1234567890", info.MobileNumber);
        Assert.True(info.IsSentSMS);
    }

    [Fact]
    public void PostmanApiConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new PostmanApiConfiguration();

        // Assert
        Assert.Equal("", config.SingleSmsUrl);
    }

    [Fact]
    public void PostmanApiConfiguration_CanSetProperties()
    {
        // Arrange
        var config = new PostmanApiConfiguration();

        // Act
        config.SingleSmsUrl = "/api/v1/sms/single";

        // Assert
        Assert.Equal("/api/v1/sms/single", config.SingleSmsUrl);
    }

    [Fact]
    public void AzureKeyVaultConfiguration_CanSetRetryOptions()
    {
        // Arrange
        var config = new AzureKeyVaultConfiguration();
        var retryOptions = new RetryOptions
        {
            Delay = 1000,
            MaxDelay = 5000,
            MaxRetries = 3,
            Mode = "Exponential"
        };

        // Act
        config.RetryOptions = retryOptions;

        // Assert
        Assert.NotNull(config.RetryOptions);
        Assert.Equal(1000, config.RetryOptions.Delay);
        Assert.Equal(5000, config.RetryOptions.MaxDelay);
        Assert.Equal(3, config.RetryOptions.MaxRetries);
        Assert.Equal("Exponential", config.RetryOptions.Mode);
    }

    [Fact]
    public void CloudRoleTelemetryInitializer_SetsRoleName()
    {
        // Arrange
        var roleName = "TestRole";
        var initializer = new CloudRoleTelemetryInitializer(roleName);
        var telemetry = new Microsoft.ApplicationInsights.DataContracts.RequestTelemetry();

        // Act
        initializer.Initialize(telemetry);

        // Assert
        Assert.Equal(roleName, telemetry.Context.Cloud.RoleName);
    }
}