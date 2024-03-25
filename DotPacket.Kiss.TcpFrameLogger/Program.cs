using System.Globalization;
using System.Net.Sockets;
using DotPacket.Kiss;

Console.WriteLine("KISS TCP Frame Logger - Logs received KISS frames as Base64 strings\n");

Console.Write("TCP host/ip> ");
string host = Console.ReadLine()!;

Console.Write("TCP port> ");
int port = int.Parse(Console.ReadLine()!);

TcpClient connection = new(host, port);
KissConsumer kissConsumer = new KissConsumer(connection.GetStream());

kissConsumer.FrameReceived += KissConsumerOnFrameReceived;

void KissConsumerOnFrameReceived(KissConsumer sender)
{
    KissFrame? frame = sender.TakeNextFrame();
    
    if (frame is not null) Console.WriteLine($"[{frame.ReceivedDate.ToString(CultureInfo.InvariantCulture)}] {frame.Command}@{frame.Address}> " + Convert.ToBase64String(frame.Data));
}

await Task.Delay(-1);