using DotPacket.Ax25.FrameParts;

namespace DotPacket.Ax25.Frames
{
    public class UnnumberedFrame(AddressFramePart source, AddressFramePart destination, AddressFramePart[] digipeaters) : BaseFrame(source, destination, digipeaters)
    {
        
    }
}