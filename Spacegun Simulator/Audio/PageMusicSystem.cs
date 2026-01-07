using Spacegun_Simulator.Audio;
using Spacegun_Simulator.Audio.Backends;
using Spacegun_Simulator.UI;
using System.Linq;
using System.Text.Json;

namespace Spacegun_Simulator
{
    /// <summary>
    /// Centralized music manager for per-page background music.
    /// Automatically handles music transitions and continues playing if the same track is requested.
    ///
    /// Behavior:
    /// - Default for pages: LoFiMusicGenerator (procedural music)
    /// - Per-page override: .wav filename (played via SoundPlayer)
    /// - Empty string "" => silence
    /// - null => use default (procedural generator)
    /// </summary>
    public static class PageMusicSystem
    {
        private static readonly string MUSIC_PATH = Path.Combine(AppContext.BaseDirectory, "Assets", "audio");
        private static string? _currentTrack; // wav filename, or null for generator, or "" for silence
        private static readonly object _lock = new object();

        // ===========================
        // Generator (default) support
        // ===========================
        private enum MusicMode { None, Wav, Generator }
        private static MusicMode _currentMode = MusicMode.None;

        private static LoFiMusicGenerator? _generator;
        private static IDisposable? _playbackHandle; // wav or generator handle
        private static IAudioBackend? _backend;

        public static bool TryGetGenerator(out LoFiMusicGenerator generator)
        {
            lock (_lock)
            {
                if (_generator == null)
                {
                    generator = null!;
                    return false;
                }

                generator = _generator;
                return true;
            }
        }

        public static LoFiMusicGenerator.AudioTuningSettings GetTuningSnapshot()
        {
            lock (_lock)
            {
                EnsureGenerator_NoLock();
                return _generator!.GetTuningSnapshot();
            }
        }

        public static void SetLayerEnabled(LoFiMusicGenerator.MusicLayer layer, bool enabled)
        {
            lock (_lock)
            {
                EnsureGenerator_NoLock();
                _generator!.SetLayerEnabled(layer, enabled);
                _generator.SaveTuning();
            }
        }

        public static void SetDrumLaneEnabled(LoFiMusicGenerator.DrumLane lane, bool enabled)
        {
            lock (_lock)
            {
                EnsureGenerator_NoLock();
                _generator!.SetDrumLaneEnabled(lane, enabled);
                _generator.SaveTuning();
            }
        }

        public static void AdjustDrumLaneGain(LoFiMusicGenerator.DrumLane lane, float delta)
        {
            lock (_lock)
            {
                EnsureGenerator_NoLock();
                _generator!.AdjustLaneGain(lane, delta);
                _generator.SaveTuning();
            }
        }

        public static void AdjustGlobal(string which, float delta)
        {
            lock (_lock)
            {
                EnsureGenerator_NoLock();
                _generator!.AdjustGlobal(which, delta);
                _generator.SaveTuning();
            }
        }

        public static void SetMelodySeedStep(int index, int? semitone)
        {
            lock (_lock)
            {
                EnsureGenerator_NoLock();
                _generator!.SetMelodySeedStep(index, semitone);
                _generator.SaveTuning();
            }
        }

        public static void SetBassPatternStep(int index, int? semitone)
        {
            lock (_lock)
            {
                EnsureGenerator_NoLock();
                _generator!.SetBassPatternStep(index, semitone);
                _generator.SaveTuning();
            }
        }

        // ===========================
        // Presets (shareable save files)
        // ===========================
        private static readonly string MUSIC_PRESETS_DIR =
            Path.Combine(Spacegun_Simulator.Core.UserDataPaths.GetSavesDirectory(), "Music");

        private static string SanitizePresetName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0) return "Preset";

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Length > 64 ? name[..64] : name;
        }

        private static void EnsureDefaultPresetExists_NoLock()
        {
            Directory.CreateDirectory(MUSIC_PRESETS_DIR);
            var existing = Directory.GetFiles(MUSIC_PRESETS_DIR, "*.json");
            if (existing.Length > 0) return;

            EnsureGenerator_NoLock();
            var snapshot = _generator!.GetTuningSnapshot();
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(MUSIC_PRESETS_DIR, "Default.json"), json);
        }

        public static string[] ListMusicPresets()
        {
            lock (_lock)
            {
                EnsureDefaultPresetExists_NoLock();
                return Directory
                    .GetFiles(MUSIC_PRESETS_DIR, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        public static bool SaveMusicPreset(string name)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(MUSIC_PRESETS_DIR);
                EnsureGenerator_NoLock();

                string safe = SanitizePresetName(name);
                string path = Path.Combine(MUSIC_PRESETS_DIR, safe + ".json");
                var snapshot = _generator!.GetTuningSnapshot();
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return true;
            }
        }

        public static bool LoadMusicPreset(string name)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(MUSIC_PRESETS_DIR);
                EnsureGenerator_NoLock();

                string safe = SanitizePresetName(name);
                string path = Path.Combine(MUSIC_PRESETS_DIR, safe + ".json");
                if (!File.Exists(path))
                    return false;

                try
                {
                    var json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<LoFiMusicGenerator.AudioTuningSettings>(json);
                    if (settings == null) return false;

                    _generator!.ApplyTuning(settings);
                    _generator.SaveTuning();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Master table of page music overrides.
        /// Format: [PageKey] = MusicFileName
        /// Just provide the filename - the path "./Assets/audio/" is added automatically.
        /// Use null to play the default (procedural generator), or "" (empty string) for silence.
        /// </summary>
        public static readonly Dictionary<string, string?> MusicTracks = new()
        {
            [DEFAULT_KEY] = null,

            [PageId.Title] = "Zarathustra.wav",
            [PageId.MainMenu] = null,
            [PageId.MusicConfiguration] = null,
            [PageId.DifficultySelection] = null,
            ["GameOver"] = "GameOverMusic.wav",
            ["Detection"] = null,
            ["ResourceAllocation"] = null,
            ["ResourceOptions"] = null,
            ["PreparationSummary"] = null,
            ["ResearchMenu"] = null,
            ["PreparationStatus"] = null,
            ["WeaponDevelopment"] = null,
            ["ProjectileDevelopment"] = null,
            ["ProjectileConfigSummary"] = null,
            ["GunDevelopment"] = null,
        };

        private const string DEFAULT_KEY = "_default";

        public static void PlayForPage(string? pageKey)
        {
            lock (_lock)
            {
                string? trackName = GetTrackForPage(pageKey);

                if (trackName == "")
                {
                    if (_currentMode != MusicMode.None || _currentTrack != "")
                        Stop();

                    _currentMode = MusicMode.None;
                    _currentTrack = "";
                    return;
                }

                if (string.IsNullOrEmpty(trackName))
                {
                    if (_currentMode == MusicMode.Generator)
                        return;

                    Stop();
                    StartGeneratorPlayback_NoLock();
                    _currentMode = MusicMode.Generator;
                    _currentTrack = null;
                    return;
                }

                if (_currentMode == MusicMode.Wav && trackName == _currentTrack)
                    return;

                Stop();

                try
                {
                    string fullPath = Path.Combine(MUSIC_PATH, trackName);
                    if (!File.Exists(fullPath))
                    {
                        _currentTrack = null;
                        _currentMode = MusicMode.None;
                        return;
                    }

                    _backend ??= CreateBackend_NoLock();
                    _playbackHandle = _backend.StartWavLooping(fullPath);
                    _currentTrack = trackName;
                    _currentMode = MusicMode.Wav;
                }
                catch
                {
                    _currentTrack = null;
                    _currentMode = MusicMode.None;
                }
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                try { _playbackHandle?.Dispose(); } catch { }
                _playbackHandle = null;

                _currentTrack = null;
                _currentMode = MusicMode.None;
            }
        }

        public static string? CurrentTrack => _currentTrack;

        public static bool IsPlaying =>
            (_currentMode == MusicMode.Wav && _playbackHandle != null && _currentTrack != null) ||
            (_currentMode == MusicMode.Generator && _generator != null && _playbackHandle != null);

        private static string? GetTrackForPage(string? pageKey)
        {
            if (string.IsNullOrWhiteSpace(pageKey))
                return MusicTracks.TryGetValue(DEFAULT_KEY, out var defaultTrack) ? defaultTrack : null;

            if (MusicTracks.TryGetValue(pageKey, out var track))
            {
                if (track == null)
                    return MusicTracks.TryGetValue(DEFAULT_KEY, out var defaultTrack) ? defaultTrack : null;

                return track;
            }

            return MusicTracks.TryGetValue(DEFAULT_KEY, out var fallbackTrack) ? fallbackTrack : null;
        }

        private static void EnsureGenerator_NoLock()
        {
            _generator ??= new LoFiMusicGenerator();
        }

        private static void StartGeneratorPlayback_NoLock()
        {
            _backend ??= CreateBackend_NoLock();
            EnsureGenerator_NoLock();
            _playbackHandle = _backend.StartProcedural(_generator!);
        }

        private static IAudioBackend CreateBackend_NoLock()
        {
            if (OperatingSystem.IsWindows())
                return WindowsAudioBackend.Instance;

            // Non-Windows builds prefer OpenAL when available, but should fail safe.
            return OpenAlAudioBackend.Instance;
        }
    }

}
