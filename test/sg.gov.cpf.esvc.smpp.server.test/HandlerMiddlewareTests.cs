using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Handlers;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using Ssg.gov.cpf.esvc.smpp.server.Middlewares;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class HandlerMiddlewareTests
{
    private readonly Mock<ILogger<HandlerMiddleware>> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockScopedProvider;
    private readonly Mock<ISmppSession> _mockSession;
    private readonly SmppServerConfiguration _config;
    private TelemetryClient _telemetryClient;

    public HandlerMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<HandlerMiddleware>>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockScopedProvider = new Mock<IServiceProvider>();
        _mockSession = new Mock<ISmppSession>();
        _config = new SmppServerConfiguration
        {
            SystemId = "test-system"
        };

        SetupServiceProvider();
    }

    [Fact]
    public async Task HandleAsync_WithBindTransceiver_UsesBindHandler()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.BindTransceiver,
            SequenceNumber = 1,
            Body = CreateBindTransceiverBody()
        };

        var mockAuthService = new Mock<IAuthenticationService>();
        mockAuthService.Setup(x => x.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var bindHandler = new BindTransceiverHandler(
            Mock.Of<ILogger<BindTransceiverHandler>>(),
            mockAuthService.Object
        );

        SetupHandlers(bindHandler, null, null, null);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SmppConstants.SmppCommandId.BindTransceiverResp, result.CommandId);
    }

    [Fact]
    public async Task HandleAsync_WithSubmitSm_UsesSubmitHandler()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.SubmitSm,
            SequenceNumber = 1,
            Body = CreateSubmitSmBody()
        };

        var mockConcatService = new Mock<IMessageConcatenationService>();
        mockConcatService
            .Setup(x => x.ProcessMessagePartAsync(
                It.IsAny<SmppConstants.ConcatenationInfo?>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<Models.DTOs.SubmitSmRequest>()))
            .ReturnsAsync(new Models.DTOs.ConcatenationResult(true, "Test"));

        var mockProcessor = new Mock<IMessageProcessor>();

        var submitHandler = new SubmitSmHandler(
            Mock.Of<ILogger<SubmitSmHandler>>(),
            mockConcatService.Object,
            mockProcessor.Object,
            _telemetryClient
        );

        SetupHandlers(null, submitHandler, null, null);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SmppConstants.SmppCommandId.SubmitSmResp, result.CommandId);
    }

    [Fact]
    public async Task HandleAsync_WithEnquireLink_UsesEnquireHandler()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink,
            SequenceNumber = 1
        };

        var enquireHandler = new EnquireLinkHandler(
            Mock.Of<ILogger<EnquireLinkHandler>>()
        );

        SetupHandlers(null, null, enquireHandler, null);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SmppConstants.SmppCommandId.EnquireLinkResp, result.CommandId);
    }

    [Fact]
    public async Task HandleAsync_WithUnbind_UsesUnbindHandler()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.Unbind,
            SequenceNumber = 1
        };

        var unbindHandler = new UnbindHandler(
            Mock.Of<ILogger<EnquireLinkHandler>>()
        );

        SetupHandlers(null, null, null, unbindHandler);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SmppConstants.SmppCommandId.UnbindResp, result.CommandId);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownCommand_ReturnsInvalidCommandError()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var unknownCommandId = 0xFFFFFFFFu;
        var pdu = new SmppPdu
        {
            CommandId = unknownCommandId,
            SequenceNumber = 1
        };

        // Setup all handlers (they will all return false for CanHandle)
        var bindHandler = new BindTransceiverHandler(
            Mock.Of<ILogger<BindTransceiverHandler>>(),
            Mock.Of<IAuthenticationService>()
        );
        var submitHandler = new SubmitSmHandler(
            Mock.Of<ILogger<SubmitSmHandler>>(),
            Mock.Of<IMessageConcatenationService>(),
            Mock.Of<IMessageProcessor>(),
            _telemetryClient
        );
        var enquireHandler = new EnquireLinkHandler(Mock.Of<ILogger<EnquireLinkHandler>>());
        var unbindHandler = new UnbindHandler(Mock.Of<ILogger<EnquireLinkHandler>>());

        SetupHandlers(bindHandler, submitHandler, enquireHandler, unbindHandler);

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SmppConstants.SmppCommandStatus.ESME_RINVCMDID, result.CommandStatus);
        Assert.Equal(pdu.SequenceNumber, result.SequenceNumber);
        Assert.Equal(unknownCommandId | 0x80000000, result.CommandId);
    }

    [Fact]
    public async Task HandleAsync_SetsSystemIdOnPdu()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink,
            SequenceNumber = 1
        };

        var enquireHandler = new EnquireLinkHandler(
            Mock.Of<ILogger<EnquireLinkHandler>>()
        );

        SetupHandlers(null, null, enquireHandler, null);

        // Act
        await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.Equal(_config.SystemId, pdu.SystemId);
    }

    [Fact]
    public async Task HandleAsync_WithNoMatchingHandler_LogsWarning()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var pdu = new SmppPdu
        {
            CommandId = 0xFFFFFFFFu,
            SequenceNumber = 1
        };

        var bindHandler = new BindTransceiverHandler(
            Mock.Of<ILogger<BindTransceiverHandler>>(),
            Mock.Of<IAuthenticationService>()
        );
        var submitHandler = new SubmitSmHandler(
            Mock.Of<ILogger<SubmitSmHandler>>(),
            Mock.Of<IMessageConcatenationService>(),
            Mock.Of<IMessageProcessor>(),
            _telemetryClient
        );
        var enquireHandler = new EnquireLinkHandler(Mock.Of<ILogger<EnquireLinkHandler>>());
        var unbindHandler = new UnbindHandler(Mock.Of<ILogger<EnquireLinkHandler>>());

        SetupHandlers(bindHandler, submitHandler, enquireHandler, unbindHandler);

        // Act
        await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No handler found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DisposesServiceScope()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var pdu = new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.EnquireLink,
            SequenceNumber = 1
        };

        var enquireHandler = new EnquireLinkHandler(
            Mock.Of<ILogger<EnquireLinkHandler>>()
        );

        SetupHandlers(null, null, enquireHandler, null);

        // Act
        await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        _mockServiceScope.Verify(x => x.Dispose(), Times.Once);
    }

    private HandlerMiddleware CreateMiddleware()
    {
        return new HandlerMiddleware(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _config
        );
    }

    private void SetupServiceProvider()
    {
        // Setup the scope factory pattern
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);

        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);

        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockScopedProvider.Object);

        var telemetryConfig = new TelemetryConfiguration();
        _telemetryClient = new TelemetryClient(telemetryConfig);
    }

    private void SetupHandlers(
        BindTransceiverHandler? bindHandler,
        SubmitSmHandler? submitHandler,
        EnquireLinkHandler? enquireHandler,
        UnbindHandler? unbindHandler)
    {
        // If handler is null, create a default one
        _mockScopedProvider
            .Setup(x => x.GetService(typeof(BindTransceiverHandler)))
            .Returns(bindHandler ?? new BindTransceiverHandler(
                Mock.Of<ILogger<BindTransceiverHandler>>(),
                Mock.Of<IAuthenticationService>()));

        _mockScopedProvider
            .Setup(x => x.GetService(typeof(SubmitSmHandler)))
            .Returns(submitHandler ?? new SubmitSmHandler(
                Mock.Of<ILogger<SubmitSmHandler>>(),
                Mock.Of<IMessageConcatenationService>(),
                Mock.Of<IMessageProcessor>(),
                _telemetryClient));

        _mockScopedProvider
            .Setup(x => x.GetService(typeof(EnquireLinkHandler)))
            .Returns(enquireHandler ?? new EnquireLinkHandler(
                Mock.Of<ILogger<EnquireLinkHandler>>()));

        _mockScopedProvider
            .Setup(x => x.GetService(typeof(UnbindHandler)))
            .Returns(unbindHandler ?? new UnbindHandler(
                Mock.Of<ILogger<EnquireLinkHandler>>()));
    }

    private byte[] CreateBindTransceiverBody()
    {
        var body = new List<byte>();
        // service_type
        body.Add(0x00);
        // system_id
        body.AddRange(System.Text.Encoding.ASCII.GetBytes("testuser"));
        body.Add(0x00);
        // password
        body.AddRange(System.Text.Encoding.ASCII.GetBytes("testpass"));
        body.Add(0x00);
        // system_type
        body.Add(0x00);
        // interface_version
        body.Add(0x34);
        // addr_ton
        body.Add(0x00);
        // addr_npi
        body.Add(0x00);
        // address_range
        body.Add(0x00);
        return body.ToArray();
    }

    private byte[] CreateSubmitSmBody()
    {
        var body = new List<byte>();
        body.Add(0x00); // service_type
        body.AddRange(new byte[] { 0x01, 0x01 }); // ton, npi
        body.AddRange(System.Text.Encoding.ASCII.GetBytes("1234567890"));
        body.Add(0x00);
        body.AddRange(new byte[] { 0x01, 0x01 }); // dest ton, npi
        body.AddRange(System.Text.Encoding.ASCII.GetBytes("0987654321"));
        body.Add(0x00);
        body.AddRange(new byte[10]); // other fields
        body.Add(0x04); // sm_length
        body.AddRange(System.Text.Encoding.ASCII.GetBytes("Test"));
        return body.ToArray();
    }
}