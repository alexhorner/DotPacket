using System.Globalization;
using System.Net.Sockets;
using DotPacket.Kiss;

Console.WriteLine("KISS TCP Frame Logger - Logs received KISS frames as Base64 strings\n");

Console.Write("TCP host/ip [127.0.0.1]> ");
string? hostLine = Console.ReadLine();
string host = string.IsNullOrWhiteSpace(hostLine) ? "127.0.0.1" : hostLine;

Console.Write("TCP port [8105]> ");
string? portLine = Console.ReadLine();
int port = string.IsNullOrWhiteSpace(portLine) ? 8105 : int.Parse(portLine);

Console.Write("Retransmit on Receive? [y/N]> ");
string? retransmitLine = Console.ReadLine();
bool retransmit = !string.IsNullOrWhiteSpace(retransmitLine) && retransmitLine is "y" or "Y";

TcpClient connection = new(host, port);
KissConsumer kissConsumer = new KissConsumer(connection.GetStream());
KissProducer kissProducer = new(connection.GetStream());

kissConsumer.FrameReceived += KissConsumerOnFrameReceived;

void KissConsumerOnFrameReceived(KissConsumer sender)
{
    KissFrame? frame = sender.TakeNextFrame();

    if (frame is not null)
    {
        Console.WriteLine($"[{frame.Created.ToString(CultureInfo.InvariantCulture)}] C:{frame.Command}@A:{frame.Address}> " + Convert.ToBase64String(frame.Data));

        if (retransmit) kissProducer.QueueFrame(frame);
    }
}

await Task.Delay(-1);