using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Services;
using Xunit;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class MessageProcessorTests
{
    private readonly Mock<ILogger<MessageProcessor>> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IDeliveryReceiptSender> _mockReceiptSender;
    private readonly Mock<IExternalMessageService> _mockExternalService;
    private readonly Mock<ISmppSession> _mockSession;
    private readonly EnvironmentVariablesConfiguration _envConfig;

    public MessageProcessorTests()
    {
        _mockLogger = new Mock<ILogger<MessageProcessor>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockReceiptSender = new Mock<IDeliveryReceiptSender>();
        _mockExternalService = new Mock<IExternalMessageService>();
        _mockSession = new Mock<ISmppSession>();

        _envConfig = new EnvironmentVariablesConfiguration
        {
            IsDeliverSmEnabled = true
        };

        // Setup service provider
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IDeliveryReceiptSender)))
            .Returns(_mockReceiptSender.Object);
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IExternalMessageService)))
            .Returns(_mockExternalService.Object);
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(EnvironmentVariablesConfiguration)))
            .Returns(_envConfig);
    }

    [Fact]
    public async Task ProcessCompleteMessageAsync_WithValidParameters_CallsExternalService()
    {
        // Arrange
        var processor = CreateProcessor();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        var message = "Test message";
        var campaignId = "campaign-123";
        var messageId = "msg-123";

        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                destinationAddress,
                message,
                campaignId,
                messageId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Models.DTOs.ExternalServiceResult(true, null, null, messageId));

        // Act
        await processor.ProcessCompleteMessageAsync(
            sourceAddress,
            destinationAddress,
            message,
            campaignId,
            messageId,
            null,
            _mockSession.Object,
            CancellationToken.None
        );

        // Assert
        _mockExternalService.Verify(
            x => x.SendMessageAsync(
                It.IsAny<string>(),
                destinationAddress,
                message,
                campaignId,
                messageId,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessCompleteMessageAsync_WithDelay_PassesDelayToExternalService()
    {
        // Arrange
        var processor = CreateProcessor();
        var sourceAddress = "1234567890";
        var destinationAddress = "0987654321";
        var message = "Test message";
        var campaignId = "campaign-123";
        var messageId = "msg-123";
        var delayInSeconds = 5;

        _mockExternalService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                destinationAddress,
                message,
                campaignId,
                messageId,
                delayInSeconds,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Models.DTOs.ExternalServiceResult(true, null, null, messageId));

        // Act
        await processor.ProcessCompleteMessageAsync(
            sourceAddress,
            destinationAddress,
            message,
            campaignId,
            messageId,
            delayInSeconds,
            _mockSession.Object,
            CancellationToken.None
        );

        // Assert
        _mockExternalService.Verify(
            x => x.SendMessageAsync(
                It.IsAny<string>(),
                destinationAddress,
                message,
                campaignId,
                messageId,
                delayInSeconds,
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private MessageProcessor CreateProcessor()
    {
        return new MessageProcessor(_mockLogger.Object, _mockServiceProvider.Object);
    }
}