using Smpp.Server.Constants;
using Smpp.Server.Models.DTOs;

namespace Smpp.Server.Interfaces;

public interface IMessageConcatenationService
{
    Task<ConcatenationResult> ProcessMessagePartAsync(
        SmppConstants.ConcatenationInfo? concatInfo,
        bool isMultipartMessage,
        string message,
        SubmitSmRequest request);
}