namespace Spacegun_Simulator.Audio.Backends;

public sealed class NullAudioBackend : IAudioBackend
{
    public static readonly NullAudioBackend Instance = new();

    private NullAudioBackend() { }

    public IDisposable? StartProcedural(LoFiMusicGenerator generator) => null;

    public IDisposable? StartWavLooping(string fullPath) => null;
}
