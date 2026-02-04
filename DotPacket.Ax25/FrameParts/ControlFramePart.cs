namespace DotPacket.Ax25.FrameParts
{
    public class ControlFramePart
    {
        public required Type FrameType { get; init; }
        public int? SendSequence { get; init; }
        public int? ReceiveSequence { get; init; }
        public required bool PollFinal { get; init; }
        public SupervisoryFrameType? SupervisoryFrameType { get; init; }
    }
}