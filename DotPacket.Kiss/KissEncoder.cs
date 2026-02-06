using System.Collections.Concurrent;

namespace DotPacket.Kiss
{
    public class KissEncoder
    {
        private readonly Stream _rawFrameDataOutput;
        private readonly ConcurrentQueue<byte[]?> _frames = new();
        private Task? _processor;
        private CancellationTokenSource _cancellationTokenSource = null!;

        public KissEncoder(Stream rawFrameDataOutput, bool startOnInitialisation = true)
        {
            _rawFrameDataOutput = rawFrameDataOutput;
            
            if (startOnInitialisation) Start();
        }
        
        public void Start()
        {
            if (_processor?.Status is TaskStatus.Running) return;
            
            _cancellationTokenSource = new CancellationTokenSource();
            _processor = ProcessAsync(_cancellationTokenSource.Token);
        }

        public async Task<Exception?> StopAsync()
        {
            if (_processor is null) return null;

            await _cancellationTokenSource.CancelAsync();
            
            while (_processor.Status is TaskStatus.Running) await Task.Yield();

            Exception? latestException = _processor.Exception;
            
            _processor = null;

            return latestException;
        }
        
        public void QueueFrame(KissFrame frame)
        {
            List<byte> frameBytes = new();
            
            frameBytes.Add(KissConstants.Fend);
            
            frameBytes.Add((byte)((frame.Address << 4) + frame.Command));
            
            frameBytes.AddRange(KissEscape(frame.Data));
            
            frameBytes.Add(KissConstants.Fend);
            
            _frames.Enqueue(frameBytes.ToArray());
        }
        
        public void QueueRawFrame(byte[] rawFrame)
        {
            if (_frames.IsEmpty) throw new ArgumentException("Frame data is empty", nameof(rawFrame));
            
            _frames.Enqueue(rawFrame);
        }

        private async Task ProcessAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
                
                if (!_frames.TryDequeue(out byte[]? frame)) continue;
                
                if (frame is null) continue;

                await _rawFrameDataOutput.WriteAsync(frame, cancellationToken);
            }
        }

        public static byte[] KissEscape(byte[] dataToEscape)
        {
            byte[] escaped = new byte[dataToEscape.Length + dataToEscape.Count(b => b is KissConstants.Fend or KissConstants.Fesc)];

            int escapedIndex = 0;
            
            // ReSharper disable once ForCanBeConvertedToForeach - More efficient
            for (int bIndex = 0; bIndex < dataToEscape.Length; bIndex++)
            {
                byte b = dataToEscape[bIndex];

                switch (b)
                {
                    case KissConstants.Fend:
                        escaped[escapedIndex++] = KissConstants.Fesc;
                        escaped[escapedIndex++] = KissConstants.Tfend;
                        break;
                    
                    case KissConstants.Fesc:
                        escaped[escapedIndex++] = KissConstants.Fesc;
                        escaped[escapedIndex++] = KissConstants.Tfesc;
                        break;
                    
                    default:
                        escaped[escapedIndex++] = b;
                        break;
                }
            }

            return escaped;
        }
    }
}