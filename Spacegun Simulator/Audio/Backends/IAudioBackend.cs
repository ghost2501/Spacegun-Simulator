namespace Spacegun_Simulator.Audio.Backends;

public interface IAudioBackend
{
    IDisposable? StartProcedural(LoFiMusicGenerator generator);
    IDisposable? StartWavLooping(string fullPath);
}
