using System.Globalization;
using System.IO.Ports;
using System.Net.Sockets;
using DotPacket.Kiss;

Console.WriteLine("KISS Frame Logger - Logs received KISS frames as Base64 strings\n");

Console.Write("(S)erial or (T)CP> ");
ConsoleKeyInfo mode = Console.ReadKey();
Console.WriteLine();

switch (mode.KeyChar)
{
    case 'S':
    case 's':
        RunSerialLogger();
        break;
    
    case 'T':
    case 't':
        RunTcpLogger();
        break;
    
    default: throw new InvalidOperationException("Unknown mode");
}

void RunSerialLogger()
{
    Console.Write("Port [/dev/ttyACM1]> ");
    string? portLine = Console.ReadLine();
    string port = string.IsNullOrWhiteSpace(portLine) ? "/dev/ttyACM1" : portLine;
    
    Console.Write("Baudrate [57600 (NiniTNC)]> ");
    string? baudLine = Console.ReadLine();
    int baud = string.IsNullOrWhiteSpace(baudLine) ? 57600 : int.Parse(baudLine!);
    
    SerialPort serial = new();
    
    serial.PortName = port;
    serial.BaudRate = baud;
    serial.DataBits = 8;
    serial.Parity = Parity.None;
    serial.StopBits = StopBits.One;
    serial.Handshake = Handshake.None;
    
    serial.Open();
    
    RunDecode(serial.BaseStream);
}

void RunTcpLogger()
{
    Console.Write("TCP host/ip [127.0.0.1]> ");
    string? hostLine = Console.ReadLine();
    string host = string.IsNullOrWhiteSpace(hostLine) ? "127.0.0.1" : hostLine;

    Console.Write("TCP port [8105]> ");
    string? portLine = Console.ReadLine();
    int port = string.IsNullOrWhiteSpace(portLine) ? 8105 : int.Parse(portLine);
    
    TcpClient connection = new(host, port);
    
    RunDecode(connection.GetStream());
}

void RunDecode(Stream stream)
{
    Console.Write("Retransmit on Receive? [y/N]> ");
    string? retransmitLine = Console.ReadLine();
    bool retransmit = !string.IsNullOrWhiteSpace(retransmitLine) && retransmitLine is "y" or "Y";

    KissDecoder kissDecoder = new KissDecoder(stream);
    KissEncoder kissEncoder = new(stream);

    kissDecoder.FrameReceived += sender =>
    {
        KissFrame? frame = sender.TakeNextFrame();

        if (frame is not null)
        {
            Console.WriteLine($"[{frame.Created.ToString(CultureInfo.InvariantCulture)}] C:{frame.Command}@A:{frame.Address}> " + Convert.ToBase64String(frame.Data));

            if (retransmit) kissEncoder.QueueFrame(frame);
        }
    };
}

await Task.Delay(-1);