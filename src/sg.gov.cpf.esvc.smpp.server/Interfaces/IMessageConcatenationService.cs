using sg.gov.cpf.esvc.smpp.server.Constants;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;

namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface IMessageConcatenationService
{
    Task<ConcatenationResult> ProcessMessagePartAsync(
        SmppConstants.ConcatenationInfo? concatInfo,
        bool isMultipartMessage,
        string message,
        SubmitSmRequest request);
}