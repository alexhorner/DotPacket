using DotPacket.Ax25.FrameParts;

namespace DotPacket.Ax25.Frames
{
    public class InformationFrame(AddressFramePart source, AddressFramePart destination, AddressFramePart[] digipeaters, byte receiveSequence, bool pollFinal, byte sendSequence) : BaseFrame(source, destination, digipeaters)
    {
        public byte ReceiveSequence { get; } = receiveSequence;
        public bool PollFinal { get; } = pollFinal;
        public byte SendSequence { get; } = sendSequence;
    }
}