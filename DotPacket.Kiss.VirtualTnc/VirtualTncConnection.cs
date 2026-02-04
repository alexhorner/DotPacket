using System.Collections.ObjectModel;
using System.Net.Sockets;

namespace DotPacket.Kiss.VirtualTnc
{
    public class VirtualTncConnection : IDisposable, IAsyncDisposable
    {
        public readonly TcpClient Client;
        public ReadOnlyCollection<KissFrame> OutstandingFrames => _kissConsumer.OutstandingFrames;
        
        private readonly KissConsumer _kissConsumer;
        private readonly KissProducer _kissProducer;
        
        public VirtualTncConnection(TcpClient client, bool startOnInitialisation = true)
        {
            Client = client;
            
            NetworkStream clientStream = client.GetStream();
            
            _kissConsumer = new KissConsumer(clientStream, startOnInitialisation);
            _kissProducer = new KissProducer(clientStream, startOnInitialisation);
        }

        public void Start()
        {
            _kissProducer.Start();
            _kissConsumer.Start();
        }

        public Task StopAsync() => Task.WhenAll([_kissProducer.StopAsync(), _kissConsumer.StopAsync()]);

        public void QueueFrame(KissFrame frame) => _kissProducer.QueueFrame(frame);
        public KissFrame? TakeNextFrame() => _kissConsumer.TakeNextFrame();
        public List<KissFrame> TakeOutstandingFrames()
        {
            List<KissFrame> frames = new();

            while (_kissConsumer.OutstandingFrames.Any())
            {
                KissFrame? frame = _kissConsumer.TakeNextFrame();
                
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