using DotPacket.Ax25.FrameParts;

namespace DotPacket.Ax25.Frames
{
    public class SupervisoryFrame(AddressFramePart source, AddressFramePart destination, AddressFramePart[] digipeaters, byte receiveSequence, bool pollFinal, SupervisoryType supervisoryType) : BaseFrame(source, destination, digipeaters)
    {
        public byte ReceiveSequence { get; } = receiveSequence;
        public bool PollFinal { get; } = pollFinal;
        public SupervisoryType SupervisoryType { get; } = supervisoryType;
    }
}