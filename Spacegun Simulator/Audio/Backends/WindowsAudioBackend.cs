using System.Media;
using System.Runtime.Versioning;

namespace Spacegun_Simulator.Audio.Backends;

[SupportedOSPlatform("windows")]
public sealed class WindowsAudioBackend : IAudioBackend
{
    public static readonly WindowsAudioBackend Instance = new();

    private WindowsAudioBackend() { }

    public IDisposable? StartProcedural(LoFiMusicGenerator generator)
    {
        try
        {
            return generator.StartPlayback();
        }
        catch
        {
            return null;
        }
    }

    public IDisposable? StartWavLooping(string fullPath)
    {
        try
        {
            if (!File.Exists(fullPath)) return null;

            var player = new SoundPlayer(fullPath);
            player.PlayLooping();
            return new SoundPlayerHandle(player);
        }
        catch
        {
            return null;
        }
    }

    private sealed class SoundPlayerHandle : IDisposable
    {
        private SoundPlayer? _player;

        public SoundPlayerHandle(SoundPlayer player)
        {
            _player = player;
        }

        public void Dispose()
        {
            try { _player?.Stop(); } catch { }
            try { _player?.Dispose(); } catch { }
            _player = null;
        }
    }
}
