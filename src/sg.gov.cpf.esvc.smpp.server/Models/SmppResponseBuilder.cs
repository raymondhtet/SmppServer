using sg.gov.cpf.esvc.smpp.server.Constants;
using System.Text;

namespace sg.gov.cpf.esvc.smpp.server.Models;

public class SmppResponseBuilder
{
    private readonly SmppPdu _response = new();

    public static SmppResponseBuilder Create() => new();

    public SmppResponseBuilder WithCommandId(uint commandId)
    {
        _response.CommandId = commandId;
        return this;
    }

    public SmppResponseBuilder WithSequenceNumber(uint sequenceNumber)
    {
        _response.SequenceNumber = sequenceNumber;
        return this;
    }

    public SmppResponseBuilder WithStatus(uint status)
    {
        _response.CommandStatus = status;
        return this;
    }

    public SmppResponseBuilder WithBody(byte[] body)
    {
        _response.Body = body;
        return this;
    }

    public SmppResponseBuilder AsSuccess()
    {
        _response.CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK;
        return this;
    }

    public SmppResponseBuilder AsError(uint errorCode)
    {
        _response.CommandStatus = errorCode;
        return this;
    }

    public SmppResponseBuilder AsBindTransceiverResponse(uint sequenceNumber, bool isSuccess, string systemId)
    {
        _response.CommandId = SmppConstants.SmppCommandId.BindTransceiverResp;
        _response.SequenceNumber = sequenceNumber;
        _response.CommandStatus = isSuccess 
            ? SmppConstants.SmppCommandStatus.ESME_ROK 
            : SmppConstants.SmppCommandStatus.ESME_RBINDFAIL;
        _response.Body = Encoding.UTF8.GetBytes(systemId + char.MinValue);
        return this;
    }

    public SmppResponseBuilder AsSubmitSmResponse(uint sequenceNumber, string messageId)
    {
        _response.CommandId = SmppConstants.SmppCommandId.SubmitSmResp;
        _response.SequenceNumber = sequenceNumber;
        _response.CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK;
        _response.Body = Encoding.ASCII.GetBytes(messageId + char.MinValue);
        return this;
    }

    public SmppResponseBuilder AsEnquireLinkResponse(uint sequenceNumber)
    {
        _response.CommandId = SmppConstants.SmppCommandId.EnquireLinkResp;
        _response.SequenceNumber = sequenceNumber;
        _response.CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK;
        return this;
    }

    public SmppResponseBuilder AsUnbindResponse(uint sequenceNumber)
    {
        _response.CommandId = SmppConstants.SmppCommandId.UnbindResp;
        _response.SequenceNumber = sequenceNumber;
        _response.CommandStatus = SmppConstants.SmppCommandStatus.ESME_ROK;
        return this;
    }

    public SmppPdu Build() => _response;

}