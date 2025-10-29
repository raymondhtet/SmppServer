using sg.gov.cpf.esvc.smpp.server.Models;

namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface ISmppSession : IDisposable
{
    string Id { get; }
    
    string SystemId { get; set; }

    Guid ProcessId { get; set; }

    bool IsAuthenticated { get; set; }
    Task<SmppPdu?> ReadPduAsync(CancellationToken cancellationToken = default);
    Task SendPduAsync(SmppPdu pdu, CancellationToken cancellationToken = default);
    void Pause();
    void Resume();
    void Close();
}