using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DotPacket.Kiss
{
    public class KissConsumer
    {
        public ReadOnlyCollection<KissFrame> OutstandingFrames => new(_frames.ToList());
        public event FrameReceivedEvent? FrameReceived;
        
        public delegate void FrameReceivedEvent(KissConsumer sender);
     
        private readonly Stream _rawFrameDataInput;
        private readonly ConcurrentQueue<KissFrame> _frames = new();
        private Task? _processor;
        private CancellationTokenSource _cancellationTokenSource = null!;

        public KissConsumer(Stream rawFrameDataInput, bool startOnInitialisation = true)
        {
            _rawFrameDataInput = rawFrameDataInput;
            
            if (startOnInitialisation) Start();
        }

        public void Start()
        {
            if (_processor?.Status is TaskStatus.Running) return;
            
            _cancellationTokenSource = new CancellationTokenSource();
            _processor = Task.Run(() => Process(_cancellationTokenSource.Token));
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

        public bool IsRunning(out Exception? latestException)
        {
            latestException = null;
            
            if (_processor is null) return false;

            if (_processor.Status is TaskStatus.Running) return true;

            latestException = _processor.Exception;
            
            return false;
        }
        
        public KissFrame? PeekNextFrame() => _frames.TryPeek(out KissFrame? frame) ? frame : null;
        public KissFrame? TakeNextFrame() => _frames.TryDequeue(out KissFrame? frame) ? frame : null;

        private void Process(CancellationToken cancellationToken)
        {
            List<byte>? framebuffer = null;
            bool escapeMode = false;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                int readByte = _rawFrameDataInput.ReadByte();

                switch (readByte)
                {
                    case -1: continue; //No byte read, try again
                    
                    case KissConstants.Fend:
                        if (escapeMode)
                        {
                            //This isn't a TFEND or TFESC whilst we're in escape mode. We'll just take the previous FESC as a normal byte and switch out of escape mode
                            framebuffer?.Add(KissConstants.Fesc);
                            escapeMode = false;
                        }

                        if (framebuffer is null)
                        {
                            //Start a new frame
                            framebuffer = new List<byte>();
                            continue;
                        }

                        if (framebuffer.Count == 0) continue; //Multiple FENDs, discard additional ones and continue to believe we're at the start of a frame
                        
                        //There's data in the frame, so this FEND must signify the end of a frame. Wrap it up and enqueue it, then empty the framebuffer to mark that we're no longer in a frame
                        _frames.Enqueue(new KissFrame(DateTime.UtcNow, framebuffer.ToArray()));
                        framebuffer = null;
                        
                        //Trigger an event task to consume the frame. This is blocking so the event should be snappy
                        try
                        {
                            FrameReceived?.Invoke(this);
                        }
                        catch
                        {
                            //Discard
                        }
                        break;
                    
                    case KissConstants.Fesc:
                        if (framebuffer is null) break; //Discard data outside of a frame
                        
                        if (escapeMode)
                        {
                            //This isn't a TFEND or TFESC whilst we're in escape mode. We'll just take the previous FESC as a normal byte and switch out of escape mode
                            framebuffer.Add(KissConstants.Fesc);
                            //escapeMode = false;
                        }

                        //The next byte will be an escaped frame control byte
                        escapeMode = true;
                        break;
                    
                    case KissConstants.Tfend:
                        if (framebuffer is null) break; //Discard data outside of a frame
                        
                        if (escapeMode)
                        {
                            //We're escaping a FEND frame control byte so provide a FEND to the framebuffer
                            framebuffer.Add(KissConstants.Fend);
                            break;
                        }

                        //We aren't escaping so accept the TFEND as a raw byte into the framebuffer
                        framebuffer.Add(KissConstants.Tfend);
                        break;
                    
                    case KissConstants.Tfesc:
                        if (framebuffer is null) break; //Discard data outside of a frame
                        
                        if (escapeMode)
                        {
                            //We're escaping a FESC frame control byte so provide a FESC to the framebuffer
                            framebuffer.Add(KissConstants.Fesc);
                            break;
                        }

                        //We aren't escaping so accept the TFESC as a raw byte into the framebuffer
                        framebuffer.Add(KissConstants.Tfesc);
                        break;
                    
                    default:
                        if (framebuffer is null) break; //Discard data outside of a frame
                        
                        if (escapeMode)
                        {
                            //Unexpected escape mode with a normal byte. Just switch out of escape mode, take the FESC as a normal byte, and accept the byte
                            escapeMode = false;
                            framebuffer.Add(KissConstants.Fesc);
                        }
                        
                        //Accept a standard raw byte into the buffer
                        framebuffer.Add((byte)readByte);
                        break;
                }
            }
        }
    }
}