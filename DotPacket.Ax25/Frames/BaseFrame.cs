using DotPacket.Ax25.FrameParts;

namespace DotPacket.Ax25.Frames
{
    public abstract class BaseFrame(AddressFramePart source, AddressFramePart destination, AddressFramePart[] digipeaters)
    {
        public AddressFramePart Source { get; } = source;
        public AddressFramePart Destination { get; } = destination;
        public AddressFramePart[] Digipeaters { get; } = digipeaters;
    }
}