using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class ProcessCompleteMessageCommandTests
{
    private readonly Mock<ISmppSession> _mockSession;
    private readonly Mock<IDeliveryReceiptSender> _mockReceiptSender;
    private readonly Mock<IExternalMessageService> _mockExternalService;
    private readonly Mock<ILogger> _mockLogger;
    private readonly EnvironmentVariablesConfiguration _envConfig;

    public ProcessCompleteMessageCommandTests()
    {
        _mockSession = new Mock<ISmppSession>();
        _mockReceiptSender = new Mock<IDeliveryReceiptSender>();
        _mockExternalService = new Mock<IExternalMessageService>();
        _mockLogger = new Mock<ILogger>();
        
        _envConfig = new EnvironmentVariablesConfiguration
        {
            IsDeliverSmEnabled = true
        };

        _mockSession.Setup(x => x.SystemId).Returns("test-system");
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulExternalService_ReturnsSuccess()
    {
        // Arrange
        var command = CreateCommand();
        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalServiceResult(true, null, null, "msg-123"));

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedExternalService_SendsDeliveryReceipt()
    {
        // Arrange
        var command = CreateCommand();
        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalServiceResult(false, "Error message", "001", null));

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        Assert.False(result.IsSuccess);
        _mockReceiptSender.Verify(
            x => x.SendDeliveryReceiptAsync(
                _mockSession.Object,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Models.DeliveryStatus>(),
                null,
                null),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ReturnsFailureAndSendsReceipt()
    {
        // Arrange
        var command = CreateCommand();
        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        _mockReceiptSender.Verify(
            x => x.SendDeliveryReceiptAsync(
                _mockSession.Object,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Models.DeliveryStatus>(),
                null,
                null),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithDeliverSmEnabled_SendsDeliveryReceipt()
    {
        // Arrange
        _envConfig.IsDeliverSmEnabled = true;
        var command = CreateCommand();
        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalServiceResult(true, null, null, "msg-123"));

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_CallsExternalServiceWithCorrectParameters()
    {
        // Arrange
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        var message = "Test message";
        var campaignId = "campaign-123";
        var messageId = "msg-123";
        var delayInSeconds = 5;

        var command = new ProcessCompleteMessageCommand(
            sourceAddress,
            destinationAddress,
            message,
            campaignId,
            messageId,
            delayInSeconds,
            _mockSession.Object,
            _mockReceiptSender.Object,
            _mockExternalService.Object,
            _envConfig,
            _mockLogger.Object
        );

        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                "test-system",
                destinationAddress,
                message,
                campaignId,
                messageId,
                delayInSeconds,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalServiceResult(true, null, null, messageId));

        // Act
        await command.ExecuteAsync();

        // Assert
        _mockExternalService.Verify(
            x => x.SendMessageAsync(
                "test-system",
                destinationAddress,
                message,
                campaignId,
                messageId,
                delayInSeconds,
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithReceiptSenderException_LogsErrorButDoesNotThrow()
    {
        // Arrange
        var command = CreateCommand();
        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalServiceResult(false, "Error", "001", null));

        _mockReceiptSender
            .Setup(x => x.SendDeliveryReceiptAsync(
                It.IsAny<ISmppSession>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Models.DeliveryStatus>(),
                null,
                null))
            .ThrowsAsync(new Exception("Receipt sender error"));

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        Assert.False(result.IsSuccess);
    }

    private ProcessCompleteMessageCommand CreateCommand()
    {
        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalServiceResult(true, null, null, "msg-123"));

        return new ProcessCompleteMessageCommand(
            "1234567890",
            "0987654321",
            "Test message",
            "campaign-123",
            "msg-123",
            null,
            _mockSession.Object,
            _mockReceiptSender.Object,
            _mockExternalService.Object,
            _envConfig,
            _mockLogger.Object
        );
    }
}