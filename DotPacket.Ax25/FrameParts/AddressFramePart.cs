namespace DotPacket.Ax25.FrameParts
{
    public record AddressFramePart
    {
        public string Callsign { get; }
        public short Ssid { get; }
        public bool CommandOrRepeated { get; }
        public bool Reserved5 { get; }
        public bool Reserved6 { get; }

        public AddressFramePart(string callsign, short ssid, bool commandOrRepeated, bool reserved5, bool reserved6)
        {
            if (string.IsNullOrWhiteSpace(callsign)) throw new ArgumentNullException(nameof(callsign));
            if (callsign.Length > 6) throw new ArgumentException("Maximum callsign length is 6", nameof(callsign));

            Callsign = callsign;
            Ssid = ssid;
            CommandOrRepeated = commandOrRepeated;
            Reserved5 = reserved5;
            Reserved6 = reserved6;
        }
    }
}