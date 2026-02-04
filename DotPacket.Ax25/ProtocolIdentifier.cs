namespace DotPacket.Ax25
{
    public enum ProtocolIdentifier
    {
        Ccitt8208X25Plp = 0x01,
        CompressedTcpIp = 0x06,
        UncompressedTcpIp = 0x07,
        SegmentationFragment = 0x08,
        TexnetDatagramProtocol = 0xC3,
        LinkQualityProtocol = 0xC4,
        Appletalk = 0xCA,
        AppletalkArp = 0xCB,
        Ip = 0xCC,
        Arp = 0xCD,
        FlexNet = 0xCE,
        NetRom = 0xCf,
        None = 0xF0,
        Escape = 0xFF
    }
}