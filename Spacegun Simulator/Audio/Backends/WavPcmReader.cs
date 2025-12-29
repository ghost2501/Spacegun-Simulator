using System.Text;

namespace Spacegun_Simulator.Audio.Backends;

public static class WavPcmReader
{
    public sealed class WavPcm16
    {
        public required int SampleRate { get; init; }
        public required int Channels { get; init; }
        public required byte[] Data { get; init; }
    }

    public static bool TryReadPcm16(string path, out WavPcm16 wav)
    {
        wav = null!;

        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

            var riff = new string(br.ReadChars(4));
            if (riff != "RIFF") return false;

            _ = br.ReadInt32(); // file size

            var wave = new string(br.ReadChars(4));
            if (wave != "WAVE") return false;

            ushort audioFormat = 0;
            ushort channels = 0;
            int sampleRate = 0;
            ushort bitsPerSample = 0;
            byte[]? data = null;

            while (br.BaseStream.Position + 8 <= br.BaseStream.Length)
            {
                var chunkId = new string(br.ReadChars(4));
                int chunkSize = br.ReadInt32();
                if (chunkSize < 0) return false;

                long chunkStart = br.BaseStream.Position;

                if (chunkId == "fmt ")
                {
                    audioFormat = br.ReadUInt16();
                    channels = br.ReadUInt16();
                    sampleRate = br.ReadInt32();
                    _ = br.ReadInt32(); // byte rate
                    _ = br.ReadUInt16(); // block align
                    bitsPerSample = br.ReadUInt16();

                    // Skip any extra fmt bytes
                    long remaining = chunkSize - 16;
                    if (remaining > 0) br.BaseStream.Seek(remaining, SeekOrigin.Current);
                }
                else if (chunkId == "data")
                {
                    data = br.ReadBytes(chunkSize);
                }
                else
                {
                    br.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                }

                // Chunks are word-aligned
                long bytesRead = br.BaseStream.Position - chunkStart;
                long toSkip = chunkSize - bytesRead;
                if (toSkip > 0) br.BaseStream.Seek(toSkip, SeekOrigin.Current);
                if ((chunkSize & 1) == 1) br.BaseStream.Seek(1, SeekOrigin.Current);

                if (data != null && audioFormat != 0 && sampleRate != 0 && bitsPerSample != 0)
                    break;
            }

            if (audioFormat != 1) return false; // PCM
            if (bitsPerSample != 16) return false;
            if (channels is < 1 or > 2) return false;
            if (sampleRate <= 0) return false;
            if (data == null || data.Length == 0) return false;

            wav = new WavPcm16
            {
                SampleRate = sampleRate,
                Channels = channels,
                Data = data
            };

            return true;
        }
        catch
        {
            return false;
        }
    }
}
