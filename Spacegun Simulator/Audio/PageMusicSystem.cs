using System.Media;
using System.Reflection;
using Spacegun_Simulator.UI;

namespace Spacegun_Simulator.Audio
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
        private static SoundPlayer? _currentPlayer;
        private static string? _currentTrack; // wav filename, or null for generator, or "" for silence
        private static readonly object _lock = new object();

        // ===========================
        // Generator (default) support
        // ===========================
        private enum MusicMode { None, Wav, Generator }
        private static MusicMode _currentMode = MusicMode.None;

        private static LoFiMusicGenerator? _generator;
        private static object? _generatorPlaybackHandle; // WaveOutEvent / IDisposable / etc.

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
                    StartGeneratorIfNeeded();
                    _currentMode = MusicMode.Generator;
                    _currentTrack = null; // null indicates generator
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
                    if (!OperatingSystem.IsWindows())
                    {
                        _currentTrack = null;
                        _currentMode = MusicMode.None;
                        return;
                    }

                    string fullPath = Path.Combine(MUSIC_PATH, trackName);

                    if (!File.Exists(fullPath))
                    {
                        _currentTrack = null;
                        _currentMode = MusicMode.None;
                        return;
                    }

                    _currentPlayer = new SoundPlayer(fullPath);
                    _currentPlayer.PlayLooping();
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

        /// <summary>
        /// Stops all music playback (wav or generator).
        /// </summary>
        public static void Stop()
        {
            lock (_lock)
            {
                // Stop wav
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        _currentPlayer?.Stop();
                        _currentPlayer?.Dispose();
                    }
                }
                catch { }
                finally
                {
                    _currentPlayer = null;
                }

                // Stop generator playback handle (if any)
                try
                {
                    if (_generatorPlaybackHandle != null)
                    {
                        InvokeIfExists(_generatorPlaybackHandle, "Stop");
                        InvokeIfExists(_generatorPlaybackHandle, "Dispose");
                    }
                }
                catch { }
                finally
                {
                    _generatorPlaybackHandle = null;
                }

                // Drop generator instance (keeps behavior simple/robust)
                _generator = null;

                _currentTrack = null;
                _currentMode = MusicMode.None;
            }
        }

        public static string? CurrentTrack => _currentTrack;

        public static bool IsPlaying =>
            (_currentMode == MusicMode.Wav && _currentPlayer != null && _currentTrack != null) ||
            (_currentMode == MusicMode.Generator && _generator != null);

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

        private static void StartGeneratorIfNeeded()
        {
            if (_generator != null)
                return;

            _generator = new LoFiMusicGenerator();

            // Prefer a non-blocking playback API if your generator has one.
            // We use reflection so PageMusicSystem compiles even if you rename generator methods.
            // Expected (recommended) API on LoFiMusicGenerator:
            //   public IDisposable StartPlayback();
            //
            // If StartPlayback isn't found, we'll log a warning and fall back to silence.
            var handle = TryInvoke(_generator, "StartPlayback");
            if (handle == null)
            {
                _generator = null;
                _generatorPlaybackHandle = null;
                _currentMode = MusicMode.None;
                _currentTrack = null;
                return;
            }

            _generatorPlaybackHandle = handle;
        }

        private static object? TryInvoke(object target, string methodName)
        {
            try
            {
                var mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
                if (mi == null) return null;
                return mi.Invoke(target, null);
            }
            catch
            {
                return null;
            }
        }

        private static void InvokeIfExists(object target, string methodName)
        {
            var mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (mi == null) return;
            mi.Invoke(target, null);
        }
    }
}
