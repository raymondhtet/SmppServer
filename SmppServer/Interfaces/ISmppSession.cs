using Smpp.Server.Models;

namespace Smpp.Server.Interfaces;

public interface ISmppSession : IDisposable
{
    string Id { get; }
    
    string SystemId { get; set; }

    bool IsAuthenticated { get; set; }
    Task<SmppPdu?> ReadPduAsync(CancellationToken cancellationToken = default);
    Task SendPduAsync(SmppPdu pdu, CancellationToken cancellationToken = default);
    void Pause();
    void Resume();
    void Close();

}