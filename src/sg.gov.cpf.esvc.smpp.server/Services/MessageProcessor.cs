using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;

namespace sg.gov.cpf.esvc.smpp.server.Services;

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
        string campaignId,
        string messageId,
        int? delayInSeconds,
        ISmppSession session,
        CancellationToken cancellationToken)
    {
        var command = new ProcessCompleteMessageCommand(
            sourceAddress,
            destinationAddress,
            message,
            campaignId,
            messageId,
            delayInSeconds,
            session,            
            _serviceProvider.GetRequiredService<IDeliveryReceiptSender>(),
            _serviceProvider.GetRequiredService<IExternalMessageService>(),
            _serviceProvider.GetRequiredService<EnvironmentVariablesConfiguration>(),
            _logger);

        await command.ExecuteAsync(cancellationToken);
    }
}