using System.Text;
using System.Text.RegularExpressions;
using DotPacket.Ax25.FrameParts;
using DotPacket.Ax25.Frames;

namespace DotPacket.Ax25
{
    public static class FrameCodec
    {
        public static (AddressFramePart Destination, AddressFramePart Source, AddressFramePart[] Digipeaters) DecodeFrameAddresses(byte[] frameBytes)
        {
            //Decode the destination
            AddressFramePart destination = DecodeAddressField(frameBytes[..6], out bool destinationAddressExtensionBitSet);

            if (destinationAddressExtensionBitSet) throw new FrameFormatException("The destination address subfield has the address extension bit set and is therefore invalid");
            
            //Decode any digipeaters, and the source
            AddressFramePart? source = null;
            List<AddressFramePart> digipeaters = [];

            int currentByteOffset = 0;
            bool noRemainingAddresses = false;

            while (!noRemainingAddresses)
            {
                currentByteOffset += 7; //Move on 7 bytes to skip to the next address field
                
                if (currentByteOffset > (7 * 3 /*Up to 3 7-byte address fields*/)) throw new FrameFormatException("Address field count has exceeded maximum allowed addresses"); //TODO AH: This would only allow for 1 repeater. The spec allows up to 8

                AddressFramePart address = DecodeAddressField(frameBytes[currentByteOffset..(currentByteOffset + 6)], out noRemainingAddresses);

                if (source is null)
                {
                    source = address;
                }
                else
                {
                    digipeaters.Add(address);
                }
            }

            if (source is null) throw new FrameFormatException("No source address field is present");

            return (destination, source, digipeaters.ToArray());
        }

        public static BaseFrame DecodeFrameRemainder(byte[] frameBytes, AddressFramePart source, AddressFramePart destination, AddressFramePart[] digipeaters, bool extendedMode)
        {
            //Figure out how many bytes to skip at the start of the frame based on how many addresses were read out
            int frameRemainderStartingIndex = 14 /*two 7-byte address fields for mandatory source and destination addresses*/ + (digipeaters.Length * 7 /*7 additional bytes for each digipeater address*/) + 1 /*we want the byte index after*/;
            int currentFrameIndex = frameRemainderStartingIndex;
            
            //Decode the control byte(s)
            byte firstControlByte;
            byte? secondControlByte = null;
            
            try
            {
                firstControlByte = frameBytes[currentFrameIndex++];

                if (extendedMode) secondControlByte = frameBytes[currentFrameIndex++];
            }
            catch (IndexOutOfRangeException)
            {
                throw new FrameFormatException("The control field is malformed");
            }

            /*//Apparrently I already wrote this 2 years ago so nevermind...
            int nreceive = 0;
            int nsend = 0;
            bool pollFinal = false;
            bool isInformation = false;

            if (extendedMode)
            {
                
            }
            else
            {
                isInformation = (firstControlByte & 0x01) == 0; //Bit 0
            }*/

            if (extendedMode)
            {
                //TODO AH: Not implemented
                //DecodeExtendedControlField(firstControlByte, secondControlByte!.Value);
                throw new NotImplementedException();
            }
            else
            {
                (Type FrameType, int? SendSequence, int? ReceiveSequence, bool PollFinal, bool UnnumberedModifiers) controlField = DecodeControlField(firstControlByte);
            }
            
            
            
            
            
            
            
            

            if (isInformation)
            {
                //Information
                return new InformationFrame(source, destination, digipeaters.ToArray());
            }
            else if (isSupervisory)
            {
                //Supervisory
                return new SupervisoryFrame(source, destination, digipeaters.ToArray());
            }
            else if (isUnnumbered)
            {
                //Unnumbered
                return new UnnumberedFrame(source, destination, digipeaters.ToArray());
            }
            else
            {
                
            }
        }

        private static AddressFramePart DecodeAddressField(byte[] field, out bool addressExtensionBitSet)
        {
            addressExtensionBitSet = false; //This bit being set means this is the last address field
            
            AddressFramePart address;
            
            try
            {
                byte[] callsignBytes = new byte[6];
                
                for (int i = 0; i < field.Length; i++)
                {
                    if (i == field.Length - 1) //is last byte
                    {
                        //If this is the last byte, store the address extension bit (bit 0) separately
                        addressExtensionBitSet = (field[i] & 0x01) > 0 /*bit 0 set*/; //TODO AH: The spec claims the last bit (extension bit) is always set but the examples it gives does not show that
                        //TODO AH: Should we do this here? Should it not be done as part of the SSID disassembly?
                    }
                    else //is not last byte
                    {
                        //Validate each address extension bits are unset, except for on the last byte
                        if ((field[i] & 0x01) > 0 /*bit 0 set*/) throw new FrameFormatException($"Byte {i} of the address field has the address extension bit set and is therefore invalid");
                    }
                    
                    //If this is a callsign byte, we'll shift it for ASCII and store it
                    if (i < 6 /*before byte 7, or [6] when 0 indexed (SSID byte)*/) callsignBytes[i] = (byte)(field[i] >> 1);
                }

                //Take the ASCII bytes for the callsign and turn them into a string, removing trailing filler spaces
                string callsign = Encoding.ASCII.GetString(callsignBytes).TrimEnd(' ');
                
                //Check the destination callsign is valid according to the spec
                if (string.IsNullOrWhiteSpace(callsign) || !Regex.IsMatch(callsign, "^[0-9A-Z]*$") /*TODO AH: Apparently we should allow things like #?*/) throw new FrameFormatException("The callsign is invalid");

                //Disassemble the SSID byte (byte 7, or [6] when 0 indexed)
                bool command = ((field[6] >> 7) & 0x01) == 1; //Bit 7
                bool reserved6 = ((field[6] >> 6) & 0x01) == 1; //Bit 6
                bool reserved5 = ((field[6] >> 5) & 0x01) == 1; //Bit 5
                short ssid = (short)((field[6] >> 1) & 0x0F); //Bits 4 to 1
                
                address = new AddressFramePart(callsign, ssid, command, reserved5, reserved6);
            }
            catch (IndexOutOfRangeException)
            {
                throw new FrameFormatException("The address field is malformed");
            }

            return address;
        }

        private static ControlFramePart DecodeControlField(byte field)
        {
            //Determine the frame type
            int frameTypeInt = field & 0x03;
            
            //Take the poll/final bit
            bool pollFinal = (field & 0x10) != 0;
            
            //Break down the other frame parts by type
            Type frameType;
            int? sendSequence = null;
            int? receiveSequence = null;
            SupervisoryFrameType? supervisoryFrameType = null;

            switch (frameTypeInt)
            {
                case 0:
                    //Information: Bit 0 and 1 are not set
                    frameType = typeof(InformationFrame);

                    receiveSequence = (field >> 5) & 0x07;
                    sendSequence = (field >> 1) & 0x07;
                    break;
                
                case 1:
                    //Supervisory: Bit 0 is set and bit 1 is unset
                    frameType = typeof(SupervisoryFrame);
                
                    receiveSequence = (field >> 5) & 0x07;
                    supervisoryFrameType = (SupervisoryFrameType)((field >> 2) & 0x03);
                    break;
                
                case 3:
                    //Unnumbered: Bit 0 and 1 are set
                    frameType = typeof(UnnumberedFrame);
                    
                    //TODO AH: Decode the M flags
                    break;
                
                default: throw new FrameFormatException("The frame type could not be determined");
            }

            return new ControlFramePart
            {
                FrameType = frameType,
                SendSequence = sendSequence,
                ReceiveSequence = receiveSequence,
                PollFinal = pollFinal,
                SupervisoryFrameType = supervisoryFrameType
            };
        }

        private static void DecodeExtendedControlField(byte fieldFirst, byte fieldSecond)
        {
            throw new NotImplementedException();
        }
    }
}