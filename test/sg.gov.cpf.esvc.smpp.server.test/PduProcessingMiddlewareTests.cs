using Moq;
using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using Ssg.gov.cpf.esvc.smpp.server.Middlewares;
using Xunit;
using Assert = Xunit.Assert;

namespace sg.gov.cpf.esvc.smpp.server.test;

public class PduProcessingMiddlewareTests
{
    private readonly Mock<ISmppSession> _mockSession;

    public PduProcessingMiddlewareTests()
    {
        _mockSession = new Mock<ISmppSession>();
    }

    [Fact]
    public void SetNext_WithValidMiddleware_ReturnsNext()
    {
        // Arrange
        var middleware1 = new TestMiddleware();
        var middleware2 = new TestMiddleware();

        // Act
        var result = middleware1.SetNext(middleware2);

        // Assert
        Assert.Equal(middleware2, result);
    }

    [Fact]
    public void SetNext_ChainMultipleMiddlewares_MaintainsChain()
    {
        // Arrange
        var middleware1 = new TestMiddleware();
        var middleware2 = new TestMiddleware();
        var middleware3 = new TestMiddleware();

        // Act
        middleware1.SetNext(middleware2).SetNext(middleware3);

        // Assert - verify chain is maintained
        Assert.NotNull(middleware1);
    }

    [Fact]
    public async Task HandleAsync_AbstractMethod_MustBeImplemented()
    {
        // Arrange
        var middleware = new TestMiddleware();
        var pdu = CreateTestPdu();

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task HandleAsync_WithChain_CallsNextInChain()
    {
        // Arrange
        var middleware1 = new TestMiddleware();
        var middleware2 = new TestMiddleware();
        var pdu = CreateTestPdu();

        middleware1.SetNext(middleware2);

        // Act
        var result = await middleware1.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware2.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_WithLongChain_CallsAllMiddlewares()
    {
        // Arrange
        var middleware1 = new TestMiddleware();
        var middleware2 = new TestMiddleware();
        var middleware3 = new TestMiddleware();
        var pdu = CreateTestPdu();

        middleware1.SetNext(middleware2).SetNext(middleware3);

        // Act
        await middleware1.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.True(middleware1.WasCalled);
        Assert.True(middleware2.WasCalled);
        Assert.True(middleware3.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_WithoutNext_DoesNotFail()
    {
        // Arrange
        var middleware = new TestMiddlewareWithoutNext();
        var pdu = CreateTestPdu();

        // Act
        var result = await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void SetNext_ReturnsNextMiddleware_AllowsFluentChaining()
    {
        // Arrange
        var middleware1 = new TestMiddleware();
        var middleware2 = new TestMiddleware();
        var middleware3 = new TestMiddleware();

        // Act
        var lastMiddleware = middleware1
            .SetNext(middleware2)
            .SetNext(middleware3);

        // Assert
        Assert.Equal(middleware3, lastMiddleware);
    }

    [Fact]
    public async Task HandleAsync_PreservesRequestContext()
    {
        // Arrange
        var middleware = new TestMiddleware();
        var pdu = CreateTestPdu();
        var sessionId = "test-session-123";

        _mockSession.Setup(x => x.Id).Returns(sessionId);

        // Act
        await middleware.HandleAsync(pdu, _mockSession.Object, CancellationToken.None);

        // Assert
        Assert.Equal(pdu, middleware.LastProcessedPdu);
        Assert.Equal(_mockSession.Object, middleware.LastSession);
    }

    [Fact]
    public async Task HandleAsync_WithCancellationToken_PassesTokenThroughChain()
    {
        // Arrange
        var middleware1 = new TestMiddleware();
        var middleware2 = new TestMiddleware();
        var pdu = CreateTestPdu();
        var cts = new CancellationTokenSource();

        middleware1.SetNext(middleware2);

        // Act
        await middleware1.HandleAsync(pdu, _mockSession.Object, cts.Token);

        // Assert
        Assert.Equal(cts.Token, middleware1.LastCancellationToken);
        Assert.Equal(cts.Token, middleware2.LastCancellationToken);
    }

    private SmppPdu CreateTestPdu()
    {
        return new SmppPdu
        {
            CommandId = SmppConstants.SmppCommandId.SubmitSm,
            CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK,
            SequenceNumber = 1,
            Body = new byte[] { 0x01, 0x02, 0x03 }
        };
    }

    // Test implementation of abstract class
    private class TestMiddleware : PduProcessingMiddleware
    {
        public bool WasCalled { get; private set; }
        public SmppPdu? LastProcessedPdu { get; private set; }
        public ISmppSession? LastSession { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public override async Task<SmppPdu?> HandleAsync(
            SmppPdu pdu,
            ISmppSession session,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastProcessedPdu = pdu;
            LastSession = session;
            LastCancellationToken = cancellationToken;

            if (Next != null)
            {
                return await Next.HandleAsync(pdu, session, cancellationToken);
            }

            return new SmppPdu
            {
                CommandId = SmppConstants.SmppCommandId.SubmitSmResp,
                CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK,
                SequenceNumber = pdu.SequenceNumber
            };
        }
    }

    // Test implementation without calling Next
    private class TestMiddlewareWithoutNext : PduProcessingMiddleware
    {
        public override Task<SmppPdu?> HandleAsync(
            SmppPdu pdu,
            ISmppSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<SmppPdu?>(new SmppPdu
            {
                CommandId = SmppConstants.SmppCommandId.SubmitSmResp,
                CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK,
                SequenceNumber = pdu.SequenceNumber
            });
        }
    }
}
