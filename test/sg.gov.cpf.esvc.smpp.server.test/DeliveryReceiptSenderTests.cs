using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class DeliveryReceiptSenderTests
{
    private readonly Mock<ILogger<DeliveryReceiptSender>> _mockLogger;
    private readonly TelemetryClient _telemetryClient;
    private readonly Mock<ISmppSession> _mockSession;

    public DeliveryReceiptSenderTests()
    {
        _mockLogger = new Mock<ILogger<DeliveryReceiptSender>>();
        
        var telemetryConfig = new TelemetryConfiguration();
        _telemetryClient = new TelemetryClient(telemetryConfig);
        
        _mockSession = new Mock<ISmppSession>();
        _mockSession.Setup(x => x.SendPduAsync(It.IsAny<SmppPdu>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task SendDeliveryReceiptAsync_WithValidParameters_SendsPdu()
    {
        // Arrange
        var sender = CreateSender();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        var messageId = "msg-123";
        var status = new DeliveryStatus() { ErrorCode = "DELIVRD", ErrorStatus = "000", MessageState = SmppConstants.MessageState.DELIVERED };

        // Act
        await sender.SendDeliveryReceiptAsync(
            _mockSession.Object,
            sourceAddress,
            destinationAddress,
            messageId,
            status
        );

        // Assert
        _mockSession.Verify(
            x => x.SendPduAsync(It.IsAny<SmppPdu>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SendDeliveryReceiptAsync_WithDates_UsesProvidedDates()
    {
        // Arrange
        var sender = CreateSender();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        var messageId = "msg-123";
        var status = new DeliveryStatus() { ErrorCode = "DELIVRD", ErrorStatus = "000", MessageState = SmppConstants.MessageState.DELIVERED };
        var submitDate = DateTime.UtcNow.AddMinutes(-5);
        var doneDate = DateTime.UtcNow;

        // Act
        await sender.SendDeliveryReceiptAsync(
            _mockSession.Object,
            sourceAddress,
            destinationAddress,
            messageId,
            status,
            submitDate,
            doneDate
        );

        // Assert
        _mockSession.Verify(
            x => x.SendPduAsync(It.IsAny<SmppPdu>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SendDeliveryReceiptAsync_WithoutDates_UsesCurrentTime()
    {
        // Arrange
        var sender = CreateSender();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        var messageId = "msg-123";
        var status = new DeliveryStatus() { ErrorCode = "DELIVRD", ErrorStatus = "000", MessageState = SmppConstants.MessageState.DELIVERED };

        // Act
        await sender.SendDeliveryReceiptAsync(
            _mockSession.Object,
            sourceAddress,
            destinationAddress,
            messageId,
            status
        );

        // Assert
        _mockSession.Verify(
            x => x.SendPduAsync(It.IsAny<SmppPdu>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SendDeliveryReceiptAsync_LogsDeliveryReceipt()
    {
        // Arrange
        var sender = CreateSender();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        var messageId = "msg-123";
        var status = new DeliveryStatus() { ErrorCode = "DELIVRD", ErrorStatus = "000", MessageState = SmppConstants.MessageState.DELIVERED };

        // Act
        await sender.SendDeliveryReceiptAsync(
            _mockSession.Object,
            sourceAddress,
            destinationAddress,
            messageId,
            status
        );

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deliver_SM Hex")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendDeliveryReceiptAsync_WithNullMessageId_SendsPdu()
    {
        // Arrange
        var sender = CreateSender();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        string? messageId = null;
        var status = new DeliveryStatus() { ErrorCode = "DELIVRD", ErrorStatus = "000", MessageState = SmppConstants.MessageState.DELIVERED };

        // Act
        await sender.SendDeliveryReceiptAsync(
            _mockSession.Object,
            sourceAddress,
            destinationAddress,
            messageId,
            status
        );

        // Assert
        _mockSession.Verify(
            x => x.SendPduAsync(It.IsAny<SmppPdu>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SendDeliveryReceiptAsync_WithFailedStatus_SendsCorrectPdu()
    {
        // Arrange
        var sender = CreateSender();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        var messageId = "msg-123";
        var status = new DeliveryStatus() { ErrorCode = "DELIVRD", ErrorStatus = "000", MessageState = SmppConstants.MessageState.DELIVERED };

        // Act
        await sender.SendDeliveryReceiptAsync(
            _mockSession.Object,
            sourceAddress,
            destinationAddress,
            messageId,
            status
        );

        // Assert
        _mockSession.Verify(
            x => x.SendPduAsync(
                It.Is<SmppPdu>(pdu => pdu.CommandId == 0x00000005), // DeliverSm
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private DeliveryReceiptSender CreateSender()
    {
        return new DeliveryReceiptSender(_mockLogger.Object, _telemetryClient);
    }
}