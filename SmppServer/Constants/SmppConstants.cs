namespace Smpp.Server.Constants;

public static class SmppConstants
{
    public static readonly int HeaderSize = 16;
    
    public enum MessageState : byte
    {
        SCHEDULED = 0,
        ENROUTE = 1,
        DELIVERED = 2,
        EXPIRED = 3,
        DELETED = 4,
        UNDELIVERABLE = 5,
        ACCEPTED = 6,
        UNKNOWN = 7,
        REJECTED = 8,
        SKIPPED = 9
    }

    public static class SmppCommandId
    {
        public const uint BindReceiver = 0x00000001;
        public const uint BindTransmitter = 0x00000002;
        public const uint QuerySm = 0x00000003;
        public const uint SubmitSm = 0x00000004;
        public const uint DeliverSm = 0x00000005;
        public const uint Unbind = 0x00000006;
        public const uint ReplaceSm = 0x00000007;
        public const uint CancelSm = 0x00000008;
        public const uint BindTransceiver = 0x00000009;
        public const uint EnquireLink = 0x00000015;

        public const uint BindReceiverResp = 0x80000001;
        public const uint BindTransmitterResp = 0x80000002;
        public const uint QuerySmResp = 0x80000003;
        public const uint SubmitSmResp = 0x80000004;
        public const uint DeliverSmResp = 0x80000005;
        public const uint UnbindResp = 0x80000006;
        public const uint ReplaceSmResp = 0x80000007;
        public const uint CancelSmResp = 0x80000008;
        public const uint BindTransceiverResp = 0x80000009;
        public const uint EnquireLinkResp = 0x80000015;
        public const uint DataSm = 0x00000103;
    }

    public static class SmppCommandStatus
    {
        public const uint ESME_ROK = 0x00000000;
        public const uint ESME_RINVMSGLEN = 0x00000001;
        public const uint ESME_RINVCMDLEN = 0x00000002;
        public const uint ESME_RINVCMDID = 0x00000003;
        public const uint ESME_RINVBNDSTS = 0x00000004;
        public const uint ESME_RALYBND = 0x00000005;
        public const uint ESME_RINVPRTFLG = 0x00000006;
        public const uint ESME_RINVREGDLVFLG = 0x00000007;
        public const uint ESME_RSYSERR = 0x00000008;
        public const uint ESME_RINVSRCADR = 0x0000000A;
        public const uint ESME_RINVDSTADR = 0x0000000B;
        public const uint ESME_RINVMSGID = 0x0000000C;
        public const uint ESME_RBINDFAIL = 0x0000000D;
        public const uint ESME_RINVPASWD = 0x0000000E;
        public const uint ESME_RINVSYSID = 0x0000000F;
    }
    
    public struct ConcatenationInfo
    {
        public ushort ReferenceNumber { get; set; }
        public byte TotalParts { get; set; }
        public byte PartNumber { get; set; }
        public ConcatenationType Type { get; set; }
    }
    
    public enum ConcatenationType
    {
        UDH,
        SAR
    }
    
    public struct UdhConcatenationData
    {
        public ushort ReferenceNumber { get; set; }
        public byte TotalParts { get; set; }
        public byte PartNumber { get; set; }
        public int UdhLength { get; set; }
    }

    public struct SarConcatenationData
    {
        public ushort ReferenceNumber { get; set; }
        public byte TotalParts { get; set; }
        public byte PartNumber { get; set; }
    }

}