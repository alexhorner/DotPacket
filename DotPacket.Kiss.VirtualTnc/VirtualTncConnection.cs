using System.Collections.ObjectModel;
using System.Net.Sockets;

namespace DotPacket.Kiss.VirtualTnc
{
    public class VirtualTncConnection : IDisposable, IAsyncDisposable
    {
        public readonly TcpClient Client;
        public ReadOnlyCollection<KissFrame> OutstandingFrames => _kissDecoder.OutstandingFrames;
        
        private readonly KissDecoder _kissDecoder;
        private readonly KissEncoder _kissEncoder;
        
        public VirtualTncConnection(TcpClient client, bool startOnInitialisation = true)
        {
            Client = client;
            
            NetworkStream clientStream = client.GetStream();
            
            _kissDecoder = new KissDecoder(clientStream, startOnInitialisation);
            _kissEncoder = new KissEncoder(clientStream, startOnInitialisation);
        }

        public void Start()
        {
            _kissEncoder.Start();
            _kissDecoder.Start();
        }

        public Task StopAsync() => Task.WhenAll([_kissEncoder.StopAsync(), _kissDecoder.StopAsync()]);

        public void QueueFrame(KissFrame frame) => _kissEncoder.QueueFrame(frame);
        public KissFrame? TakeNextFrame() => _kissDecoder.TakeNextFrame();
        public List<KissFrame> TakeOutstandingFrames()
        {
            List<KissFrame> frames = new();

            while (_kissDecoder.OutstandingFrames.Any())
            {
                KissFrame? frame = _kissDecoder.TakeNextFrame();
                
                if (frame is not null) frames.Add(frame);
            }

            return frames;
        }

        public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            Client.Dispose();
        }
    }
}