using NAudio.Wave;
using OpenTK.Audio.OpenAL;
using System.Threading;

namespace Spacegun_Simulator.Audio.Backends;

public sealed class OpenAlAudioBackend : IAudioBackend
{
    public static readonly OpenAlAudioBackend Instance = new();

    private OpenAlAudioBackend() { }

    public IDisposable? StartProcedural(LoFiMusicGenerator generator)
    {
        try
        {
            var provider = generator.CreateWaveProvider();
            return OpenAlHandle.StartStreaming(provider);
        }
        catch (Exception ex)
        {
            AudioBackendDiagnostics.LogOnce(
                key: "openal-procedural-exception",
                message: $"OpenAL procedural start failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    public IDisposable? StartWavLooping(string fullPath)
    {
        try
        {
            if (!File.Exists(fullPath)) return null;
            if (!WavPcmReader.TryReadPcm16(fullPath, out var wav)) return null;

            return OpenAlHandle.StartStaticLoop(wav.SampleRate, wav.Channels, wav.Data);
        }
        catch (Exception ex)
        {
            AudioBackendDiagnostics.LogOnce(
                key: "openal-wav-exception",
                message: $"OpenAL wav start failed ({Path.GetFileName(fullPath)}): {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private sealed class OpenAlHandle : IDisposable
    {
        private readonly ManualResetEventSlim _started = new(false);
        private Exception? _startError;
        private Thread? _thread;
        private volatile bool _stop;

        private readonly IWaveProvider? _streamProvider;
        private readonly int _wavSampleRate;
        private readonly int _wavChannels;
        private readonly byte[]? _wavData;
        private readonly bool _isStreaming;

        private OpenAlHandle(IWaveProvider streamProvider)
        {
            _isStreaming = true;
            _streamProvider = streamProvider;
        }

        private OpenAlHandle(int sampleRate, int channels, byte[] data)
        {
            _isStreaming = false;
            _wavSampleRate = sampleRate;
            _wavChannels = channels;
            _wavData = data;
        }

        public static OpenAlHandle? StartStreaming(IWaveProvider provider)
        {
            var handle = new OpenAlHandle(provider);
            return handle.StartThread();
        }

        public static OpenAlHandle? StartStaticLoop(int sampleRate, int channels, byte[] data)
        {
            var handle = new OpenAlHandle(sampleRate, channels, data);
            return handle.StartThread();
        }

        private OpenAlHandle? StartThread()
        {
            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = _isStreaming ? "OpenAL Procedural" : "OpenAL WAV"
            };
            _thread.Start();

            _started.Wait(TimeSpan.FromSeconds(2));
            if (_startError != null)
            {
                AudioBackendDiagnostics.LogOnce(
                    key: _isStreaming ? "openal-procedural-init" : "openal-wav-init",
                    message: $"OpenAL init failed ({(_isStreaming ? "procedural" : "wav")}): {_startError.GetType().Name}: {_startError.Message}");
                Dispose();
                return null;
            }

            return this;
        }

        public void Dispose()
        {
            _stop = true;
            try
            {
                if (_thread != null && _thread.IsAlive)
                    _thread.Join(TimeSpan.FromSeconds(2));
            }
            catch { }
        }

        private void ThreadMain()
        {
            ALDevice device = default;
            ALContext context = default;
            int source = 0;
            int[]? buffers = null;
            int staticBuffer = 0;

            try
            {
                device = ALC.OpenDevice(null);
                if (device.Handle == IntPtr.Zero)
                    throw new InvalidOperationException("OpenAL: failed to open device");

                unsafe
                {
                    context = ALC.CreateContext(device, (int*)null);
                }

                if (context.Handle == IntPtr.Zero)
                    throw new InvalidOperationException("OpenAL: failed to create context");

                if (!ALC.MakeContextCurrent(context))
                    throw new InvalidOperationException("OpenAL: failed to make context current");

                source = AL.GenSource();
                ALHelper.CheckError("GenSource");

                if (_isStreaming)
                {
                    var provider = _streamProvider!;
                    var fmt = provider.WaveFormat;
                    var alFormat = ToAlFormat(fmt);

                    const int bufferCount = 4;
                    const int chunkBytes = 8192;

                    buffers = AL.GenBuffers(bufferCount);
                    ALHelper.CheckError("GenBuffers");

                    var chunk = new byte[chunkBytes];
                    for (int i = 0; i < bufferCount; i++)
                    {
                        FillStreamChunk(provider, chunk);
                        BufferDataPinned(buffers[i], alFormat, chunk, fmt.SampleRate);
                        ALHelper.CheckError("BufferData(init)");
                        AL.SourceQueueBuffer(source, buffers[i]);
                        ALHelper.CheckError("SourceQueueBuffer(init)");
                    }

                    AL.SourcePlay(source);
                    ALHelper.CheckError("SourcePlay");

                    _started.Set();

                    while (!_stop)
                    {
                        AL.GetSource(source, ALGetSourcei.BuffersProcessed, out int processed);
                        ALHelper.CheckError("GetSource(BuffersProcessed)");

                        while (processed-- > 0)
                        {
                            int buf = AL.SourceUnqueueBuffer(source);
                            ALHelper.CheckError("SourceUnqueueBuffer");

                            FillStreamChunk(provider, chunk);
                            BufferDataPinned(buf, alFormat, chunk, fmt.SampleRate);
                            ALHelper.CheckError("BufferData(refill)");
                            AL.SourceQueueBuffer(source, buf);
                            ALHelper.CheckError("SourceQueueBuffer(refill)");
                        }

                        AL.GetSource(source, ALGetSourcei.SourceState, out int state);
                        if ((ALSourceState)state != ALSourceState.Playing)
                        {
                            AL.SourcePlay(source);
                        }

                        Thread.Sleep(10);
                    }
                }
                else
                {
                    var data = _wavData!;
                    var alFormat = _wavChannels switch
                    {
                        1 => ALFormat.Mono16,
                        2 => ALFormat.Stereo16,
                        _ => throw new InvalidOperationException("OpenAL: unsupported channels")
                    };

                    staticBuffer = AL.GenBuffer();
                    ALHelper.CheckError("GenBuffer");
                    BufferDataPinned(staticBuffer, alFormat, data, _wavSampleRate);
                    ALHelper.CheckError("BufferData(wav)");

                    AL.Source(source, ALSourcei.Buffer, staticBuffer);
                    AL.Source(source, ALSourceb.Looping, true);
                    AL.SourcePlay(source);
                    ALHelper.CheckError("SourcePlay(wav)");

                    _started.Set();

                    while (!_stop)
                        Thread.Sleep(25);
                }
            }
            catch (Exception ex)
            {
                _startError = ex;
                _started.Set();
            }
            finally
            {
                try { if (source != 0) AL.SourceStop(source); } catch { }

                if (buffers != null)
                {
                    try
                    {
                        AL.GetSource(source, ALGetSourcei.BuffersQueued, out int queued);
                        while (queued-- > 0)
                            _ = AL.SourceUnqueueBuffer(source);
                    }
                    catch { }

                    try { AL.DeleteBuffers(buffers); } catch { }
                }

                try { if (staticBuffer != 0) AL.DeleteBuffer(staticBuffer); } catch { }
                try { if (source != 0) AL.DeleteSource(source); } catch { }

                try { ALC.MakeContextCurrent(default); } catch { }
                try { if (context.Handle != IntPtr.Zero) ALC.DestroyContext(context); } catch { }
                try { if (device.Handle != IntPtr.Zero) ALC.CloseDevice(device); } catch { }
            }
        }

        private static unsafe void BufferDataPinned(int buffer, ALFormat format, byte[] data, int sampleRate)
        {
            fixed (byte* pData = data)
            {
                AL.BufferData(buffer, format, (nint)pData, data.Length, sampleRate);
            }
        }

        private static void FillStreamChunk(IWaveProvider provider, byte[] chunk)
        {
            int offset = 0;
            while (offset < chunk.Length)
            {
                int read = provider.Read(chunk, offset, chunk.Length - offset);
                if (read <= 0)
                {
                    Array.Clear(chunk, offset, chunk.Length - offset);
                    return;
                }
                offset += read;
            }
        }

        private static ALFormat ToAlFormat(WaveFormat fmt)
        {
            if (fmt.Encoding != WaveFormatEncoding.Pcm)
                throw new NotSupportedException("OpenAL: only PCM supported");

            return (fmt.Channels, fmt.BitsPerSample) switch
            {
                (1, 16) => ALFormat.Mono16,
                (2, 16) => ALFormat.Stereo16,
                _ => throw new NotSupportedException($"OpenAL: unsupported format {fmt.Channels}ch {fmt.BitsPerSample}bit")
            };
        }

        private static class ALHelper
        {
            public static void CheckError(string where)
            {
                var err = AL.GetError();
                if (err != ALError.NoError)
                    throw new InvalidOperationException($"OpenAL error at {where}: {err}");
            }
        }
    }
}
