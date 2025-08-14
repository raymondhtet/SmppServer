using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Interfaces;

public interface IMessageConcatenationService
{
    Task<ConcatenationResult> ProcessMessagePartAsync(SubmitSmRequest request);
}