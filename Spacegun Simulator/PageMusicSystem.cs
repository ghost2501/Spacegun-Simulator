using Spacegun_Simulator.UI;
using Spacegun_Simulator.Audio;
using Spacegun_Simulator.Audio.Backends;
using System.Text.Json;
using System.Linq;

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
            // Default music for most pages:
            // null => procedural generator
            [DEFAULT_KEY] = null,

            // Page-specific tracks
            // Page-specific tracks (legacy keys + new UI PageId keys)
            [ PageId.Title] = "Zarathustra.wav",
            [ PageId.MainMenu] = null,
            [ PageId.MusicConfiguration ] = null,
            [ PageId.DifficultySelection] = null,
            ["GameOver"] = "GameOverMusic.wav",
            ["Detection"] = null,  // Use default (generator)
            ["ResourceAllocation"] = null,
            ["ResourceOptions"] = null,
            ["PreparationSummary"] = null,
            ["ResearchMenu"] = null,
            ["PreparationStatus"] = null,

            ["WeaponDevelopment"] = null,
            ["ProjectileDevelopment"] = null,
            ["ProjectileConfigSummary"] = null,
            ["GunDevelopment"] = null,

                        // ["Silence"] = "", // Empty string for no music
        };

        private const string DEFAULT_KEY = "_default";

        /// <summary>
        /// Plays music for the given page. If the same track is already playing, does nothing.
        /// Supports .wav files only for per-page overrides (SoundPlayer limitation).
        /// Default is procedural (LoFiMusicGenerator).
        /// </summary>
        public static void PlayForPage(string? pageKey)
        {
            lock (_lock)
            {
             
                // Get the track for this page (or default)
                string? trackName = GetTrackForPage(pageKey);

                // Empty string means silence - don't start anything
                if (trackName == "")
                {
                    if (_currentMode != MusicMode.None || _currentTrack != "")
                        Stop();

                    _currentMode = MusicMode.None;
                    _currentTrack = "";
                    return;
                }

                // null means use default (procedural generator)
                if (string.IsNullOrEmpty(trackName))
                {
                    // If generator already running, do nothing
                    if (_currentMode == MusicMode.Generator)
                    {
                        return;
                    }

                    // Switch to generator
                    Stop(); // stop wav or anything else
                    EnsureGenerator_NoLock();
                    _playbackHandle = GetBackend_NoLock().StartProcedural(_generator!);
                    if (_playbackHandle != null)
                    {
                        _currentMode = MusicMode.Generator;
                        _currentTrack = null; // null indicates generator
                    }
                    else
                    {
                        _currentMode = MusicMode.None;
                        _currentTrack = null;
                    }
                    return;
                }

                // If it's the same wav already playing, do nothing
                if (_currentMode == MusicMode.Wav && trackName == _currentTrack)
                {
                    return;
                }

                // Switch to wav
                Stop();

                // Try to play the new track
                try
                {
                    string fullPath = Path.Combine(MUSIC_PATH, trackName);

                    if (!File.Exists(fullPath))
                    {
                        _currentTrack = null;
                        _currentMode = MusicMode.None;
                        return;
                    }

                    _playbackHandle = GetBackend_NoLock().StartWavLooping(fullPath);
                    if (_playbackHandle != null)
                    {
                        _currentTrack = trackName;
                        _currentMode = MusicMode.Wav;
                    }
                    else
                    {
                        _currentTrack = null;
                        _currentMode = MusicMode.None;
                    }
                }
                catch
                {
                    _currentTrack = null;
                    _currentMode = MusicMode.None;
                }
            }
        }

        /// <summary>
        /// Stops all music playback (wav or generator).
        /// </summary>
        public static void Stop()
        {
            lock (_lock)
            {
                // Stop current playback handle (wav or generator)
                try
                {
                    _playbackHandle?.Dispose();
                }
                catch { }
                finally
                {
                    _playbackHandle = null;
                }

                // Drop generator instance (keeps behavior simple/robust)
                _generator = null;

                _currentTrack = null;
                _currentMode = MusicMode.None;
            }
        }

        public static string? CurrentTrack => _currentTrack;

        public static bool IsPlaying =>
            (_currentMode == MusicMode.Wav && _playbackHandle != null && _currentTrack != null) ||
            (_currentMode == MusicMode.Generator && _playbackHandle != null && _generator != null);

        // ===========================
        // Internals
        // ===========================
        private static string? GetTrackForPage(string? pageKey)
        {
            if (string.IsNullOrWhiteSpace(pageKey))
                return MusicTracks.TryGetValue(DEFAULT_KEY, out var defaultTrack) ? defaultTrack : null;

            if (MusicTracks.TryGetValue(pageKey, out var track))
            {
                // null means use default
                if (track == null)
                    return MusicTracks.TryGetValue(DEFAULT_KEY, out var defaultTrack) ? defaultTrack : null;

                return track;
            }

            // Page not in table, use default
            return MusicTracks.TryGetValue(DEFAULT_KEY, out var fallbackTrack) ? fallbackTrack : null;
        }

        private static void EnsureGenerator_NoLock()
        {
            if (_generator != null)
                return;

            _generator = new LoFiMusicGenerator();
        }

        private static IAudioBackend GetBackend_NoLock()
        {
            if (_backend != null) return _backend;

            if (OperatingSystem.IsWindows())
            {
                _backend = WindowsAudioBackend.Instance;
                return _backend;
            }

            // Linux/macOS: prefer OpenAL, fall back to silence if unavailable.
            _backend = OpenAlAudioBackend.Instance;
            return _backend;
        }
    }
}
