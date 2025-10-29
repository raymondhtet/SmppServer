﻿using static sg.gov.cpf.esvc.smpp.server.Constants.SmppConstants;

namespace sg.gov.cpf.esvc.smpp.server.Models;

public class DeliveryStatus
{
    public MessageState MessageState { get; set; }
    public string? ErrorStatus { get; set; }
    public string? ErrorCode { get; set; }
}