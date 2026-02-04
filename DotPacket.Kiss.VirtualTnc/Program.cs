using System.Net;
using System.Net.Sockets;
using DotPacket.Kiss;
using DotPacket.Kiss.VirtualTnc;

Console.WriteLine("KISS Virtual TNC - Connects two or more KISS applications together as if they were all connected to radios on the same packet frequency\n");

Console.Write("TCP listen port [8105]> ");
string? portLine = Console.ReadLine();
int port = string.IsNullOrWhiteSpace(portLine) ? 8105 : int.Parse(portLine);

TcpListener server = new(IPAddress.Any, port);

server.Start();

List<VirtualTncConnection> connections = new();
Task _ = ProcessFramesAsync();

async Task ProcessFramesAsync()
{
    try
    {
        while (true)
        {
            await Task.Yield();

            //Clear lost connections
            List<VirtualTncConnection> deadConnections = connections.Where(c => !c.Client.Connected).ToList();

            foreach (VirtualTncConnection connection in deadConnections)
            {
                try
                {
                    await connection.DisposeAsync();
                }
                catch
                {
                    //Do nothing
                }

                connections.Remove(connection);

                Console.WriteLine($"Connection lost from {((IPEndPoint)connection.Client.Client.RemoteEndPoint!).Address.ToString()}:{((IPEndPoint)connection.Client.Client.RemoteEndPoint!).Port} ({connections.Count} connection{(connections.Count == 1 ? "" : "s")})");
            }

            //Go through each virtual TNC and collect its frames
            List<(VirtualTncConnection Tnc, KissFrame Frame)> frames = connections
                .Where(c => c.Client.Connected)
                .SelectMany<VirtualTncConnection, (VirtualTncConnection Tnc, KissFrame Frame)>(c => c.TakeOutstandingFrames().Select(f => (c, f)).ToList())
                .ToList();

            //Cancel if no frames
            if (frames.Count == 0) continue;

            //Filter out non data frames
            frames = frames.Where(f => f.Frame.Command == 0x00).ToList();

            //Order frames
            frames = frames.OrderBy(f => f.Frame.Created).ToList();

            //Handle those frames
            foreach ((VirtualTncConnection? tnc, KissFrame? frame) in frames)
            {
                Console.WriteLine($"{((IPEndPoint)tnc.Client.Client.RemoteEndPoint!).Address.ToString()}:{((IPEndPoint)tnc.Client.Client.RemoteEndPoint!).Port} >> C:{frame.Command}@A:{frame.Address} = {Convert.ToBase64String(frame.Data)}");

                //Filter out the frame originator
                List<VirtualTncConnection> targets = connections.Where(c => !Equals(c, tnc)).ToList();

                foreach (VirtualTncConnection target in targets)
                {
                    if (target.Client.Connected) target.QueueFrame(frame);
                }
            }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine("Exception occured in processor: " + e);
        Environment.Exit(1);
    }
}

while (true)
{
    TcpClient client = await server.AcceptTcpClientAsync();

    connections.Add(new VirtualTncConnection(client));
    
    Console.WriteLine($"Connection established from {((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString()}:{((IPEndPoint)client.Client.RemoteEndPoint!).Port} ({connections.Count} connection{(connections.Count == 1 ? "" : "s")})");
}