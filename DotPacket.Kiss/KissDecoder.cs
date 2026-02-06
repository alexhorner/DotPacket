using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DotPacket.Kiss
{
    public class KissDecoder
    {
        public ReadOnlyCollection<KissFrame> OutstandingFrames => new(_frames.ToList());
        public event FrameReceivedEvent? FrameReceived;
        
        public delegate void FrameReceivedEvent(KissDecoder sender);
     
        private readonly Stream _rawFrameDataInput;
        private readonly ConcurrentQueue<KissFrame> _frames = new();
        private Task? _processor;
        private CancellationTokenSource _cancellationTokenSource = null!;

        public KissDecoder(Stream rawFrameDataInput, bool startOnInitialisation = true)
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
            byte[] readBuffer = new byte[1024];
            List<byte>? frameBuffer = null;
            bool escapeMode = false;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                int readCount = _rawFrameDataInput.Read(readBuffer, 0, readBuffer.Length);

                for (int buffIdx = 0; buffIdx < readCount; buffIdx++)
                {
                    switch (readBuffer[buffIdx])
                    {
                        //case -1: continue; //No byte read, try again
                        
                        case KissConstants.Fend:
                            if (escapeMode)
                            {
                                //This isn't a TFEND or TFESC whilst we're in escape mode. We'll just take the previous FESC as a normal byte and switch out of escape mode
                                frameBuffer?.Add(KissConstants.Fesc);
                                escapeMode = false;
                            }

                            if (frameBuffer is null)
                            {
                                //Start a new frame
                                frameBuffer = new List<byte>();
                                continue;
                            }

                            if (frameBuffer.Count == 0) continue; //Multiple FENDs, discard additional ones and continue to believe we're at the start of a frame
                            
                            //There's data in the frame, so this FEND must signify the end of a frame. Wrap it up and enqueue it, then empty the framebuffer to mark that we're no longer in a frame
                            _frames.Enqueue(new KissFrame(DateTime.UtcNow, frameBuffer.ToArray()));
                            frameBuffer = null;
                            escapeMode = false;
                            
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
                            if (frameBuffer is null) break; //Discard data outside of a frame
                            
                            if (escapeMode)
                            {
                                //This isn't a TFEND or TFESC whilst we're in escape mode. We'll just take the previous FESC as a normal byte
                                frameBuffer.Add(KissConstants.Fesc);
                            }

                            //The next byte will be an escaped frame control byte
                            escapeMode = true;
                            break;
                        
                        case KissConstants.Tfend:
                            if (frameBuffer is null) break; //Discard data outside of a frame
                            
                            if (escapeMode)
                            {
                                //We're escaping a FEND frame control byte so provide a FEND to the framebuffer
                                escapeMode = false;
                                frameBuffer.Add(KissConstants.Fend);
                                break;
                            }

                            //We aren't escaping so accept the TFEND as a raw byte into the framebuffer
                            frameBuffer.Add(KissConstants.Tfend);
                            break;
                        
                        case KissConstants.Tfesc:
                            if (frameBuffer is null) break; //Discard data outside of a frame
                            
                            if (escapeMode)
                            {
                                //We're escaping a FESC frame control byte so provide a FESC to the framebuffer
                                escapeMode = false;
                                frameBuffer.Add(KissConstants.Fesc);
                                break;
                            }

                            //We aren't escaping so accept the TFESC as a raw byte into the framebuffer
                            frameBuffer.Add(KissConstants.Tfesc);
                            break;
                        
                        default:
                            if (frameBuffer is null) break; //Discard data outside of a frame
                            
                            if (escapeMode)
                            {
                                //Unexpected escape mode with a normal byte. Just switch out of escape mode, take the FESC as a normal byte, and accept the byte
                                escapeMode = false;
                                frameBuffer.Add(KissConstants.Fesc);
                            }
                            
                            //Accept a standard raw byte into the buffer
                            frameBuffer.Add(readBuffer[buffIdx]);
                            break;
                    }
                }
            }
        }
    }
}