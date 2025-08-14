using Smpp.Server.Interfaces;

namespace Smpp.Server.Services;

public class MessageProcessor : IMessageProcessor
{
    private readonly ILogger<MessageProcessor> _logger;
    private readonly IServiceProvider _serviceProvider;

    public MessageProcessor(ILogger<MessageProcessor> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task ProcessCompleteMessageAsync(
        string sourceAddress,
        string destinationAddress,
        string message,
        ISmppSession session,
        CancellationToken cancellationToken)
    {
        var command = new ProcessCompleteMessageCommand(
            sourceAddress,
            destinationAddress,
            message,
            session,
            _serviceProvider.GetRequiredService<IDeliveryReceiptSender>(),
            _serviceProvider.GetRequiredService<IExternalMessageService>(),
            _logger);

        await command.ExecuteAsync(cancellationToken);
    }
}