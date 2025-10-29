using sg.gov.cpf.esvc.smpp.server.Helpers;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Models.DTOs;

namespace sg.gov.cpf.esvc.smpp.server.Factories;

public static class SmppPduFactory
{
    public static BindTransceiverRequest CreateBindTransceiver(SmppPdu pdu)
    {
        var parser = new PduFieldParser(pdu.Body!);
        return new BindTransceiverRequest(
            ServiceType: "",
            SystemId: parser.ReadCString(),
            Password: parser.ReadCString(),
            SystemType: parser.ReadCString(),
            InterfaceVersion: parser.ReadByte(),
            AddrTon: parser.ReadByte(),
            AddrNpi: parser.ReadByte(),
            AddressRange: parser.ReadCString()
        );
    }

    public static SubmitSmRequest CreateSubmitSm(SmppPdu pdu)
    {
        var parser = new PduFieldParser(pdu.Body!);

        var request = new SubmitSmRequest
        {
            ServiceType = parser.ReadCString(),
            SourceAddrTon = parser.ReadByte(),
            SourceAddrNpi = parser.ReadByte(),
            SourceAddress = parser.ReadCString(),
            DestAddrTon = parser.ReadByte(),
            DestAddrNpi = parser.ReadByte(),
            DestinationAddress = parser.ReadCString(),
            EsmClass = parser.ReadByte(),
            ProtocolId = parser.ReadByte(),
            PriorityFlag = parser.ReadByte(),
            ScheduleDeliveryTime = parser.ReadCString(),
            ValidityPeriod = parser.ReadCString(),
            RegisteredDelivery = parser.ReadByte(),
            ReplaceIfPresentFlag = parser.ReadByte(),
            DataCoding = parser.ReadByte(),
            SmDefaultMsgId = parser.ReadByte(),
            ShortMessage = parser.ReadShortMessage(),
            OptionalParameters = parser.OptionalParameters
        };

        parser.ParseOptionalParameters();
        request.CampaignId = parser.ReadCampaignId();
        request.MessagePayload = parser.ReadMessagePayload();


        return request;
    }
}