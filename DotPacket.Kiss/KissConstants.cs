using System.Diagnostics.CodeAnalysis;

namespace DotPacket.Kiss
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public class KissConstants
    {
        public const byte Fend = 0xC0;
        public const byte Fesc = 0xDB;
        public const byte Tfend = 0xDC;
        public const byte Tfesc = 0xDD;
    }
}