namespace DotPacket.Kiss
{
    public record KissFrame
    {
        public DateTime Created { get; }
        public int Address { get; set; }
        public int Command { get; set; }
        public byte[] Data { get; }
        
        public KissFrame(DateTime created, byte[] rawFrame)
        {
            Created = created;
            Data = rawFrame[1..];
            Address = rawFrame[0] >> 4;
            Command = rawFrame[0] & 0x0F;
        }
    }
}