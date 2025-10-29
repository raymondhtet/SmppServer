namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface IMessageCommand<TResult>
{
    Task<TResult> ExecuteAsync(CancellationToken cancellationToken = default);
}