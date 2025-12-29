using NAudio.Wave;
using System.Text.Json;

namespace Spacegun_Simulator.Audio
{
public class LoFiMusicGenerator
{
    private const int SampleRate = 22050;
    private readonly Random _random = new Random();

    private double _phase = 0;
    private double _bassPhase = 0;
    private double _noisePhase = 0;
    private double _bellPhase = 0;
    private double _padPhase = 0;
    private double _lead1Phase = 0;
    private int _currentChordIndex = 0;
    private int _samplesInCurrentChord = 0;
    private int _samplesPerChord;
    private double _bpm = 95;
    private int _beatCounter = 0;
    private double _crackleIntensity = 0;
    private int _samplesPerBeat;
    private int _samplesSinceBeat = 0;
    private int _measuresPlayed = 0;
    private int _barsSinceChange = 0;

    // --- Bebop lead melody state ---
    private int _currentLeadMidiNote;
    private bool _leadInitialized = false;
    private int _leadNoteSamplesRemaining = 0;

    // ================= MIXER CONTROLS =================

    // ============================================================================
    // TUNING / MANUAL MODE (for quick sound testing and persistence)
    // ============================================================================
    private readonly object _tuningLock = new object();
    private AudioTuningSettings _tuning = new AudioTuningSettings();

    // Manual mode: solo a chosen drum lane and tweak parameters live.
    private volatile bool _manualMode = false;
    private volatile DrumLane _manualLane = DrumLane.BD;
    private int _manualHitRemaining = 0;  // samples remaining in the current manual hit
    private int _manualHitTotal = 0;
    private int _manualTriggerEveryBeats = 1; // 1=every beat, 2=every 2 beats, etc.

    private static readonly string TuningPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_tuning.json");

    // Drum lanes we want to support from pattern files (and for solo testing).
    public enum DrumLane
    {
        CH, OH, CY, CB, CP, RS, HT, MT, LT, SD, BD, AC, GH
    }

    public enum MusicLayer
    {
        Chords,
        Bass,
        Pad,
        Bell,
        LeadMelody,
        Drums,
        VinylCrackle,
        BitCrush,

        // Sub-toggles used by procedural/pattern drums.
        HiHat,
        Ride
    }

    public sealed class AudioTuningSettings
    {
        // Global tempo
        public float Bpm { get; set; } = 95.0f;

        // Independent drum tempo (allows intentional desync)
        public float DrumBpm { get; set; } = 95.0f;

        // Per-layer levels ("instrument volumes")
        public float ChordLevel { get; set; } = 0.02f;
        public float PadLevel { get; set; } = 0.06f;
        public float BassLevel { get; set; } = 0.18f;
        public float BellLevel { get; set; } = 0.03f;
        public float LeadLevel { get; set; } = 0.25f;

        public float DrumMaster { get; set; } = 1.0f;
        public float Master { get; set; } = 0.50f;

        // Effects / processing
        // Normalized 0..1 (0=muffled, 1=wide open)
        public float LowPass { get; set; } = 1.0f;

        // Simple delay
        public float DelayMix { get; set; } = 0.0f;
        public float DelayFeedback { get; set; } = 0.25f;
        public float DelayTimeMs { get; set; } = 250.0f;

        // Bitcrush: bits + dry/wet mix
        public int BitCrushBits { get; set; } = 8;
        public float BitCrushMix { get; set; } = 1.0f;

        // Vinyl crackle amplitude multiplier
        public float CrackleLevel { get; set; } = 1.0f;

        // Placeholders for future per-instrument FX (exposed in UI as sliders)
        public float ReverbMix { get; set; } = 0.0f;
        public float ChorusMix { get; set; } = 0.0f;

        // Lead melody generator controls
        public float MelodyDensity { get; set; } = 0.70f; // chance to start a note on each beat
        public float MelodyMutation { get; set; } = 0.20f; // 0..1 probability of small drift per note
        public bool MelodyUseSeed { get; set; } = true;
        public int MelodySeedLength { get; set; } = 16;

        // Nullable semitone offsets relative to root (null = rest)
        public int?[] MelodySeed { get; set; } = new int?[]
        {
            0, null, 7, null, 3, null, 10, null,
            7, null, 3, null, 0, null, 7, null
        };

        // Melody-follow controls (tie other layers to the melody backbone)
        public float ChordsMelodyFollow { get; set; } = 0.20f;
        public float ChordsMelodyDrift { get; set; } = 0.05f;
        public float ChordsMelodyMutation { get; set; } = 0.05f;

        public float BassMelodyFollow { get; set; } = 0.30f;
        public float BassMelodyDrift { get; set; } = 0.06f;
        public float BassMelodyMutation { get; set; } = 0.08f;

        // Bass local step-pattern (nullable semitone offsets relative to current chord root; null = rest)
        public int BassPatternLength { get; set; } = 16;
        public int?[] BassPattern { get; set; } = new int?[]
        {
            0, null, 0, null, 0, null, -2, null,
            0, null, 3, null, 0, null, -2, null
        };

        public float PadMelodyFollow { get; set; } = 0.40f;
        public float PadMelodyDrift { get; set; } = 0.04f;
        public float PadMelodyMutation { get; set; } = 0.04f;

        public float BellMelodyFollow { get; set; } = 0.55f;
        public float BellMelodyDrift { get; set; } = 0.06f;
        public float BellMelodyMutation { get; set; } = 0.10f;

        // Per-lane gain multipliers (1.0 = default)
        public Dictionary<string, float> DrumGains { get; set; } = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { "CH", 0.7f }, { "OH", 0.5f }, { "CY", 0.3f }, { "CB", 0.4f }, { "CP", 0.3f }, { "RS", 0.5f },
        { "HT", 0.7f }, { "MT", 0.7f }, { "LT", 0.9f }, { "SD", 0.25f }, { "BD", 0.5f }, { "AC", 0.2f }, { "GH", 1.0f },
    };

        // Stream toggles ("mix some but not all")
        public bool EnableChords { get; set; } = true;
        public bool EnableBass { get; set; } = true;
        public bool EnablePad { get; set; } = true;
        public bool EnableBell { get; set; } = true;
        public bool EnableLeadMelody { get; set; } = true;
        public bool EnableDrums { get; set; } = true;
        public bool EnableVinylCrackle { get; set; } = true;
        public bool EnableBitCrush { get; set; } = true;

        // Drum sub-toggles
        public bool EnableHiHat { get; set; } = true;
        public bool EnableRide { get; set; } = true;

        // Per-lane enable (distinct from gain so toggling doesn't destroy the tuned value)
        public Dictionary<string, bool> DrumEnabled { get; set; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            { "CH", true }, { "OH", true }, { "CY", true }, { "CB", true }, { "CP", true }, { "RS", true },
            { "HT", true }, { "MT", true }, { "LT", true }, { "SD", true }, { "BD", true }, { "AC", true }, { "GH", true },
        };
    }

    private static void EnsureLaneKeys(AudioTuningSettings settings)
    {
        if (settings.DrumGains == null)
            settings.DrumGains = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        else if (settings.DrumGains.Comparer != StringComparer.OrdinalIgnoreCase)
            settings.DrumGains = new Dictionary<string, float>(settings.DrumGains, StringComparer.OrdinalIgnoreCase);

        if (settings.DrumEnabled == null)
            settings.DrumEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        else if (settings.DrumEnabled.Comparer != StringComparer.OrdinalIgnoreCase)
            settings.DrumEnabled = new Dictionary<string, bool>(settings.DrumEnabled, StringComparer.OrdinalIgnoreCase);

        foreach (DrumLane lane in Enum.GetValues(typeof(DrumLane)))
        {
            string key = lane.ToString();
            if (!settings.DrumGains.ContainsKey(key)) settings.DrumGains[key] = 1.0f;
            if (!settings.DrumEnabled.ContainsKey(key)) settings.DrumEnabled[key] = true;
        }
    }

    private volatile bool _enableChords = true;
    private volatile bool _enableBass = true;
    private volatile bool _enablePad = true;
    private volatile bool _enableBell = true;
    private volatile bool _enableLeadMelody = true;
    private volatile bool _enableDrums = true;
    private volatile bool _enableVinylCrackle = true;
    private volatile bool _enableBitCrush = true;
    private volatile bool _enableHiHat = true;
    private volatile bool _enableRide = true;

    private bool IsLaneEnabled(DrumLane lane)
    {
        lock (_tuningLock)
        {
            return _tuning.DrumEnabled.TryGetValue(lane.ToString(), out var enabled) ? enabled : true;
        }
    }

    public bool IsLayerEnabled(MusicLayer layer)
    {
        lock (_tuningLock)
        {
            return layer switch
            {
                MusicLayer.Chords => _tuning.EnableChords,
                MusicLayer.Bass => _tuning.EnableBass,
                MusicLayer.Pad => _tuning.EnablePad,
                MusicLayer.Bell => _tuning.EnableBell,
                MusicLayer.LeadMelody => _tuning.EnableLeadMelody,
                MusicLayer.Drums => _tuning.EnableDrums,
                MusicLayer.VinylCrackle => _tuning.EnableVinylCrackle,
                MusicLayer.BitCrush => _tuning.EnableBitCrush,
                MusicLayer.HiHat => _tuning.EnableHiHat,
                MusicLayer.Ride => _tuning.EnableRide,
                _ => true
            };
        }
    }

    public void SetLayerEnabled(MusicLayer layer, bool enabled)
    {
        lock (_tuningLock)
        {
            switch (layer)
            {
                case MusicLayer.Chords:
                    _tuning.EnableChords = enabled;
                    _enableChords = enabled;
                    break;
                case MusicLayer.Bass:
                    _tuning.EnableBass = enabled;
                    _enableBass = enabled;
                    break;
                case MusicLayer.Pad:
                    _tuning.EnablePad = enabled;
                    _enablePad = enabled;
                    break;
                case MusicLayer.Bell:
                    _tuning.EnableBell = enabled;
                    _enableBell = enabled;
                    break;
                case MusicLayer.LeadMelody:
                    _tuning.EnableLeadMelody = enabled;
                    _enableLeadMelody = enabled;
                    break;
                case MusicLayer.Drums:
                    _tuning.EnableDrums = enabled;
                    _enableDrums = enabled;
                    break;
                case MusicLayer.VinylCrackle:
                    _tuning.EnableVinylCrackle = enabled;
                    _enableVinylCrackle = enabled;
                    break;
                case MusicLayer.BitCrush:
                    _tuning.EnableBitCrush = enabled;
                    _enableBitCrush = enabled;
                    break;
                case MusicLayer.HiHat:
                    _tuning.EnableHiHat = enabled;
                    _enableHiHat = enabled;
                    break;
                case MusicLayer.Ride:
                    _tuning.EnableRide = enabled;
                    _enableRide = enabled;
                    break;
            }
        }
    }

    public bool IsDrumLaneEnabled(DrumLane lane)
        => IsLaneEnabled(lane);

    public void SetDrumLaneEnabled(DrumLane lane, bool enabled)
    {
        lock (_tuningLock)
        {
            _tuning.DrumEnabled[lane.ToString()] = enabled;
        }
    }

    private void LoadTuningIfPresent()
    {
        try
        {
            if (!File.Exists(TuningPath)) return;
            var json = File.ReadAllText(TuningPath);
            var loaded = JsonSerializer.Deserialize<AudioTuningSettings>(json);
            if (loaded == null) return;

            EnsureLaneKeys(loaded);

            lock (_tuningLock)
            {
                _tuning = loaded;

                _bpm = Math.Clamp(_tuning.Bpm, 40.0f, 200.0f);
                _tuning.Bpm = (float)_bpm;
                RecomputeTiming();

                _drumBpm = Math.Clamp(_tuning.DrumBpm, 40.0f, 240.0f);
                _tuning.DrumBpm = _drumBpm;
                RecomputeDrumTiming();

                _chordLevel = _tuning.ChordLevel;
                _padLevel = _tuning.PadLevel;
                _bassLevel = _tuning.BassLevel;
                _bellLevel = _tuning.BellLevel;
                _leadLevel = _tuning.LeadLevel;

                _lowPass = Math.Clamp(_tuning.LowPass, 0.0f, 1.0f);
                _delayMix = Math.Clamp(_tuning.DelayMix, 0.0f, 1.0f);
                _delayFeedback = Math.Clamp(_tuning.DelayFeedback, 0.0f, 0.98f);
                _delayTimeMs = Math.Clamp(_tuning.DelayTimeMs, 0.0f, 1800.0f);
                RecomputeDelaySamples(_delayTimeMs);
                _bitCrushBits = Math.Clamp(_tuning.BitCrushBits, 4, 16);
                _bitCrushMix = Math.Clamp(_tuning.BitCrushMix, 0.0f, 1.0f);
                _crackleLevel = Math.Clamp(_tuning.CrackleLevel, 0.0f, 4.0f);

                _reverbMix = Math.Clamp(_tuning.ReverbMix, 0.0f, 1.0f);
                _chorusMix = Math.Clamp(_tuning.ChorusMix, 0.0f, 1.0f);

                _melodyDensity = Math.Clamp(_tuning.MelodyDensity, 0.0f, 1.0f);
                _melodyMutation = Math.Clamp(_tuning.MelodyMutation, 0.0f, 1.0f);
                _melodyUseSeed = _tuning.MelodyUseSeed;
                _melodySeedLength = Math.Clamp(_tuning.MelodySeedLength, 4, 64);
                _melodySeed = (_tuning.MelodySeed ?? Array.Empty<int?>()).ToArray();
                NormalizeMelodySeed_NoLock();

                _bassPatternLength = Math.Clamp(_tuning.BassPatternLength, 4, 64);
                _bassPattern = (_tuning.BassPattern ?? Array.Empty<int?>()).ToArray();
                NormalizeBassPattern_NoLock();

                _chordsMelodyFollow = Math.Clamp(_tuning.ChordsMelodyFollow, 0.0f, 1.0f);
                _chordsMelodyDrift = Math.Clamp(_tuning.ChordsMelodyDrift, 0.0f, 1.0f);
                _chordsMelodyMutation = Math.Clamp(_tuning.ChordsMelodyMutation, 0.0f, 1.0f);

                _bassMelodyFollow = Math.Clamp(_tuning.BassMelodyFollow, 0.0f, 1.0f);
                _bassMelodyDrift = Math.Clamp(_tuning.BassMelodyDrift, 0.0f, 1.0f);
                _bassMelodyMutation = Math.Clamp(_tuning.BassMelodyMutation, 0.0f, 1.0f);

                _padMelodyFollow = Math.Clamp(_tuning.PadMelodyFollow, 0.0f, 1.0f);
                _padMelodyDrift = Math.Clamp(_tuning.PadMelodyDrift, 0.0f, 1.0f);
                _padMelodyMutation = Math.Clamp(_tuning.PadMelodyMutation, 0.0f, 1.0f);

                _bellMelodyFollow = Math.Clamp(_tuning.BellMelodyFollow, 0.0f, 1.0f);
                _bellMelodyDrift = Math.Clamp(_tuning.BellMelodyDrift, 0.0f, 1.0f);
                _bellMelodyMutation = Math.Clamp(_tuning.BellMelodyMutation, 0.0f, 1.0f);

                _enableChords = _tuning.EnableChords;
                _enableBass = _tuning.EnableBass;
                _enablePad = _tuning.EnablePad;
                _enableBell = _tuning.EnableBell;
                _enableLeadMelody = _tuning.EnableLeadMelody;
                _enableDrums = _tuning.EnableDrums;
                _enableVinylCrackle = _tuning.EnableVinylCrackle;
                _enableBitCrush = _tuning.EnableBitCrush;
                _enableHiHat = _tuning.EnableHiHat;
                _enableRide = _tuning.EnableRide;
            }

           
        }
        catch
        {
        }
    }

    public void SaveTuning()
    {
        try
        {
            AudioTuningSettings snapshot;
            lock (_tuningLock)
            {
                _tuning.Bpm = (float)_bpm;
                _tuning.DrumBpm = _drumBpm;
                _tuning.ChordLevel = _chordLevel;
                _tuning.PadLevel = _padLevel;
                _tuning.BassLevel = _bassLevel;
                _tuning.BellLevel = _bellLevel;
                _tuning.LeadLevel = _leadLevel;

                _tuning.LowPass = _lowPass;
                _tuning.DelayMix = _delayMix;
                _tuning.DelayFeedback = _delayFeedback;
                _tuning.DelayTimeMs = _delayTimeMs;
                _tuning.BitCrushBits = _bitCrushBits;
                _tuning.BitCrushMix = _bitCrushMix;
                _tuning.CrackleLevel = _crackleLevel;

                _tuning.ReverbMix = _reverbMix;
                _tuning.ChorusMix = _chorusMix;

                _tuning.MelodyDensity = _melodyDensity;
                _tuning.MelodyMutation = _melodyMutation;
                _tuning.MelodyUseSeed = _melodyUseSeed;
                _tuning.MelodySeedLength = _melodySeedLength;
                _tuning.MelodySeed = _melodySeed.ToArray();

                _tuning.BassPatternLength = _bassPatternLength;
                _tuning.BassPattern = _bassPattern.ToArray();

                _tuning.ChordsMelodyFollow = _chordsMelodyFollow;
                _tuning.ChordsMelodyDrift = _chordsMelodyDrift;
                _tuning.ChordsMelodyMutation = _chordsMelodyMutation;

                _tuning.BassMelodyFollow = _bassMelodyFollow;
                _tuning.BassMelodyDrift = _bassMelodyDrift;
                _tuning.BassMelodyMutation = _bassMelodyMutation;

                _tuning.PadMelodyFollow = _padMelodyFollow;
                _tuning.PadMelodyDrift = _padMelodyDrift;
                _tuning.PadMelodyMutation = _padMelodyMutation;

                _tuning.BellMelodyFollow = _bellMelodyFollow;
                _tuning.BellMelodyDrift = _bellMelodyDrift;
                _tuning.BellMelodyMutation = _bellMelodyMutation;
                snapshot = _tuning;
            }

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(TuningPath, json);

        }
        catch 
        {
        }
    }

    public void EnterManualMode()
    {
        _manualMode = true;
        _manualLane = DrumLane.BD;
        _manualHitRemaining = 0;
        _manualHitTotal = 0;
        _manualTriggerEveryBeats = 1;
    }

    public void ExitManualMode()
    {
        _manualMode = false;
        _manualHitRemaining = 0;
        _manualHitTotal = 0;
    }

    public void SetManualLane(DrumLane lane)
    {
        _manualLane = lane;
    }

    public void SetManualTriggerEveryBeats(int beats)
    {
        if (beats < 1) beats = 1;
        if (beats > 8) beats = 8;
        _manualTriggerEveryBeats = beats;
    }

    public float GetLaneGain(DrumLane lane)
    {
        lock (_tuningLock)
        {
            if (_tuning.DrumGains.TryGetValue(lane.ToString(), out var g)) return g;
            return 1.0f;
        }
    }

    public void AdjustLaneGain(DrumLane lane, float delta)
    {
        lock (_tuningLock)
        {
            var key = lane.ToString();
            if (!_tuning.DrumGains.TryGetValue(key, out var g)) g = 1.0f;
            g = Math.Clamp(g + delta, 0.0f, 4.0f);
            _tuning.DrumGains[key] = g;
        }
    }

    public void AdjustGlobal(string which, float delta)
    {
        lock (_tuningLock)
        {
            switch (which)
            {
                case "BPM":
                    _bpm = Math.Clamp(_bpm + delta, 40.0, 200.0);
                    _tuning.Bpm = (float)_bpm;
                    RecomputeTiming();
                    break;

                case "DrumBPM":
                    _drumBpm = Math.Clamp(_drumBpm + delta, 40.0f, 240.0f);
                    _tuning.DrumBpm = _drumBpm;
                    RecomputeDrumTiming();
                    break;
                case "Chord": _chordLevel = Math.Clamp(_chordLevel + delta, 0.0f, 0.25f); break;
                case "Pad": _padLevel = Math.Clamp(_padLevel + delta, 0.0f, 1f); break;
                case "Bass": _bassLevel = Math.Clamp(_bassLevel + delta, 0.0f, 1.0f); break;
                case "Bell": _bellLevel = Math.Clamp(_bellLevel + delta, 0.0f, 1.0f); break;
                case "Lead": _leadLevel = Math.Clamp(_leadLevel + delta, 0.0f, 0.5f); break;
                case "DrumMaster": _tuning.DrumMaster = Math.Clamp(_tuning.DrumMaster + delta, 0.0f, 4.0f); break;
                case "Master": _tuning.Master = Math.Clamp(_tuning.Master + delta, 0.0f, 4.0f); break;

                case "LowPass":
                    _lowPass = Math.Clamp(_lowPass + delta, 0.0f, 1.0f);
                    _tuning.LowPass = _lowPass;
                    break;
                case "DelayMix":
                    _delayMix = Math.Clamp(_delayMix + delta, 0.0f, 1.0f);
                    _tuning.DelayMix = _delayMix;
                    break;
                case "DelayFeedback":
                    _delayFeedback = Math.Clamp(_delayFeedback + delta, 0.0f, 0.98f);
                    _tuning.DelayFeedback = _delayFeedback;
                    break;
                case "DelayTimeMs":
                    _delayTimeMs = Math.Clamp(_delayTimeMs + delta, 0.0f, 1800.0f);
                    _tuning.DelayTimeMs = _delayTimeMs;
                    RecomputeDelaySamples(_delayTimeMs);
                    break;
                case "BitCrushMix":
                    _bitCrushMix = Math.Clamp(_bitCrushMix + delta, 0.0f, 1.0f);
                    _tuning.BitCrushMix = _bitCrushMix;
                    break;
                case "BitCrushBits":
                    _bitCrushBits = Math.Clamp(_bitCrushBits + (int)MathF.Round(delta), 4, 16);
                    _tuning.BitCrushBits = _bitCrushBits;
                    break;
                case "Crackle":
                    _crackleLevel = Math.Clamp(_crackleLevel + delta, 0.0f, 4.0f);
                    _tuning.CrackleLevel = _crackleLevel;
                    break;

                case "ReverbMix":
                    _reverbMix = Math.Clamp(_reverbMix + delta, 0.0f, 1.0f);
                    _tuning.ReverbMix = _reverbMix;
                    break;

                case "ChorusMix":
                    _chorusMix = Math.Clamp(_chorusMix + delta, 0.0f, 1.0f);
                    _tuning.ChorusMix = _chorusMix;
                    break;

                case "MelodyDensity":
                    _melodyDensity = Math.Clamp(_melodyDensity + delta, 0.0f, 1.0f);
                    _tuning.MelodyDensity = _melodyDensity;
                    break;

                case "MelodyMutation":
                    _melodyMutation = Math.Clamp(_melodyMutation + delta, 0.0f, 1.0f);
                    _tuning.MelodyMutation = _melodyMutation;
                    break;

                case "MelodyUseSeed":
                    _melodyUseSeed = !_melodyUseSeed;
                    _tuning.MelodyUseSeed = _melodyUseSeed;
                    break;

                case "MelodySeedLength":
                    _melodySeedLength = (int)Math.Clamp(_melodySeedLength + (int)MathF.Round(delta), 4, 64);
                    _tuning.MelodySeedLength = _melodySeedLength;
                    NormalizeMelodySeed_NoLock();
                    break;

                case "BassPatternLength":
                    _bassPatternLength = (int)Math.Clamp(_bassPatternLength + (int)MathF.Round(delta), 4, 64);
                    _tuning.BassPatternLength = _bassPatternLength;
                    NormalizeBassPattern_NoLock();
                    break;

                case "ChordsMelodyFollow":
                    _chordsMelodyFollow = Math.Clamp(_chordsMelodyFollow + delta, 0.0f, 1.0f);
                    _tuning.ChordsMelodyFollow = _chordsMelodyFollow;
                    break;
                case "ChordsMelodyDrift":
                    _chordsMelodyDrift = Math.Clamp(_chordsMelodyDrift + delta, 0.0f, 1.0f);
                    _tuning.ChordsMelodyDrift = _chordsMelodyDrift;
                    break;
                case "ChordsMelodyMutation":
                    _chordsMelodyMutation = Math.Clamp(_chordsMelodyMutation + delta, 0.0f, 1.0f);
                    _tuning.ChordsMelodyMutation = _chordsMelodyMutation;
                    break;

                case "BassMelodyFollow":
                    _bassMelodyFollow = Math.Clamp(_bassMelodyFollow + delta, 0.0f, 1.0f);
                    _tuning.BassMelodyFollow = _bassMelodyFollow;
                    break;
                case "BassMelodyDrift":
                    _bassMelodyDrift = Math.Clamp(_bassMelodyDrift + delta, 0.0f, 1.0f);
                    _tuning.BassMelodyDrift = _bassMelodyDrift;
                    break;
                case "BassMelodyMutation":
                    _bassMelodyMutation = Math.Clamp(_bassMelodyMutation + delta, 0.0f, 1.0f);
                    _tuning.BassMelodyMutation = _bassMelodyMutation;
                    break;

                case "PadMelodyFollow":
                    _padMelodyFollow = Math.Clamp(_padMelodyFollow + delta, 0.0f, 1.0f);
                    _tuning.PadMelodyFollow = _padMelodyFollow;
                    break;
                case "PadMelodyDrift":
                    _padMelodyDrift = Math.Clamp(_padMelodyDrift + delta, 0.0f, 1.0f);
                    _tuning.PadMelodyDrift = _padMelodyDrift;
                    break;
                case "PadMelodyMutation":
                    _padMelodyMutation = Math.Clamp(_padMelodyMutation + delta, 0.0f, 1.0f);
                    _tuning.PadMelodyMutation = _padMelodyMutation;
                    break;

                case "BellMelodyFollow":
                    _bellMelodyFollow = Math.Clamp(_bellMelodyFollow + delta, 0.0f, 1.0f);
                    _tuning.BellMelodyFollow = _bellMelodyFollow;
                    break;
                case "BellMelodyDrift":
                    _bellMelodyDrift = Math.Clamp(_bellMelodyDrift + delta, 0.0f, 1.0f);
                    _tuning.BellMelodyDrift = _bellMelodyDrift;
                    break;
                case "BellMelodyMutation":
                    _bellMelodyMutation = Math.Clamp(_bellMelodyMutation + delta, 0.0f, 1.0f);
                    _tuning.BellMelodyMutation = _bellMelodyMutation;
                    break;
            }
        }
    }

    public AudioTuningSettings GetTuningSnapshot()
    {
        lock (_tuningLock)
        {
            // shallow clone for display
            return new AudioTuningSettings
            {
                Bpm = (float)_bpm,
                DrumBpm = _drumBpm,
                ChordLevel = _chordLevel,
                PadLevel = _padLevel,
                BassLevel = _bassLevel,
                BellLevel = _bellLevel,
                LeadLevel = _leadLevel,
                DrumMaster = _tuning.DrumMaster,
                Master = _tuning.Master,
                DrumGains = new Dictionary<string, float>(_tuning.DrumGains, StringComparer.OrdinalIgnoreCase),
                DrumEnabled = new Dictionary<string, bool>(_tuning.DrumEnabled, StringComparer.OrdinalIgnoreCase),

                LowPass = _lowPass,
                DelayMix = _delayMix,
                DelayFeedback = _delayFeedback,
                DelayTimeMs = _delayTimeMs,
                BitCrushBits = _bitCrushBits,
                BitCrushMix = _bitCrushMix,
                CrackleLevel = _crackleLevel,

                ReverbMix = _reverbMix,
                ChorusMix = _chorusMix,

                MelodyDensity = _melodyDensity,
                MelodyMutation = _melodyMutation,
                MelodyUseSeed = _melodyUseSeed,
                MelodySeedLength = _melodySeedLength,
                MelodySeed = _melodySeed.ToArray(),

                BassPatternLength = _bassPatternLength,
                BassPattern = _bassPattern.ToArray(),

                ChordsMelodyFollow = _chordsMelodyFollow,
                ChordsMelodyDrift = _chordsMelodyDrift,
                ChordsMelodyMutation = _chordsMelodyMutation,

                BassMelodyFollow = _bassMelodyFollow,
                BassMelodyDrift = _bassMelodyDrift,
                BassMelodyMutation = _bassMelodyMutation,

                PadMelodyFollow = _padMelodyFollow,
                PadMelodyDrift = _padMelodyDrift,
                PadMelodyMutation = _padMelodyMutation,

                BellMelodyFollow = _bellMelodyFollow,
                BellMelodyDrift = _bellMelodyDrift,
                BellMelodyMutation = _bellMelodyMutation,

                EnableChords = _tuning.EnableChords,
                EnableBass = _tuning.EnableBass,
                EnablePad = _tuning.EnablePad,
                EnableBell = _tuning.EnableBell,
                EnableLeadMelody = _tuning.EnableLeadMelody,
                EnableDrums = _tuning.EnableDrums,
                EnableVinylCrackle = _tuning.EnableVinylCrackle,
                EnableBitCrush = _tuning.EnableBitCrush,
                EnableHiHat = _tuning.EnableHiHat,
                EnableRide = _tuning.EnableRide
            };
        }
    }

    public void ApplyTuning(AudioTuningSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        EnsureLaneKeys(settings);

        lock (_tuningLock)
        {
            _tuning = settings;

            _bpm = Math.Clamp(_tuning.Bpm, 40.0f, 200.0f);
            _tuning.Bpm = (float)_bpm;
            RecomputeTiming();

            _drumBpm = Math.Clamp(_tuning.DrumBpm, 40.0f, 240.0f);
            _tuning.DrumBpm = _drumBpm;
            RecomputeDrumTiming();

            _chordLevel = _tuning.ChordLevel;
            _padLevel = _tuning.PadLevel;
            _bassLevel = _tuning.BassLevel;
            _bellLevel = _tuning.BellLevel;
            _leadLevel = _tuning.LeadLevel;

            _lowPass = Math.Clamp(_tuning.LowPass, 0.0f, 1.0f);
            _delayMix = Math.Clamp(_tuning.DelayMix, 0.0f, 1.0f);
            _delayFeedback = Math.Clamp(_tuning.DelayFeedback, 0.0f, 0.98f);
            _delayTimeMs = Math.Clamp(_tuning.DelayTimeMs, 0.0f, 1800.0f);
            RecomputeDelaySamples(_delayTimeMs);
            _bitCrushBits = Math.Clamp(_tuning.BitCrushBits, 4, 16);
            _bitCrushMix = Math.Clamp(_tuning.BitCrushMix, 0.0f, 1.0f);
            _crackleLevel = Math.Clamp(_tuning.CrackleLevel, 0.0f, 4.0f);

            _reverbMix = Math.Clamp(_tuning.ReverbMix, 0.0f, 1.0f);
            _chorusMix = Math.Clamp(_tuning.ChorusMix, 0.0f, 1.0f);

            _melodyDensity = Math.Clamp(_tuning.MelodyDensity, 0.0f, 1.0f);
            _melodyMutation = Math.Clamp(_tuning.MelodyMutation, 0.0f, 1.0f);
            _melodyUseSeed = _tuning.MelodyUseSeed;
            _melodySeedLength = Math.Clamp(_tuning.MelodySeedLength, 4, 64);
            _melodySeed = (_tuning.MelodySeed ?? Array.Empty<int?>()).ToArray();
            NormalizeMelodySeed_NoLock();

            _bassPatternLength = Math.Clamp(_tuning.BassPatternLength, 4, 64);
            _bassPattern = (_tuning.BassPattern ?? Array.Empty<int?>()).ToArray();
            NormalizeBassPattern_NoLock();

            _chordsMelodyFollow = Math.Clamp(_tuning.ChordsMelodyFollow, 0.0f, 1.0f);
            _chordsMelodyDrift = Math.Clamp(_tuning.ChordsMelodyDrift, 0.0f, 1.0f);
            _chordsMelodyMutation = Math.Clamp(_tuning.ChordsMelodyMutation, 0.0f, 1.0f);

            _bassMelodyFollow = Math.Clamp(_tuning.BassMelodyFollow, 0.0f, 1.0f);
            _bassMelodyDrift = Math.Clamp(_tuning.BassMelodyDrift, 0.0f, 1.0f);
            _bassMelodyMutation = Math.Clamp(_tuning.BassMelodyMutation, 0.0f, 1.0f);

            _padMelodyFollow = Math.Clamp(_tuning.PadMelodyFollow, 0.0f, 1.0f);
            _padMelodyDrift = Math.Clamp(_tuning.PadMelodyDrift, 0.0f, 1.0f);
            _padMelodyMutation = Math.Clamp(_tuning.PadMelodyMutation, 0.0f, 1.0f);

            _bellMelodyFollow = Math.Clamp(_tuning.BellMelodyFollow, 0.0f, 1.0f);
            _bellMelodyDrift = Math.Clamp(_tuning.BellMelodyDrift, 0.0f, 1.0f);
            _bellMelodyMutation = Math.Clamp(_tuning.BellMelodyMutation, 0.0f, 1.0f);

            _enableChords = _tuning.EnableChords;
            _enableBass = _tuning.EnableBass;
            _enablePad = _tuning.EnablePad;
            _enableBell = _tuning.EnableBell;
            _enableLeadMelody = _tuning.EnableLeadMelody;
            _enableDrums = _tuning.EnableDrums;
            _enableVinylCrackle = _tuning.EnableVinylCrackle;
            _enableBitCrush = _tuning.EnableBitCrush;
            _enableHiHat = _tuning.EnableHiHat;
            _enableRide = _tuning.EnableRide;
        }
    }


    private float _padLevel = 0.12f;
    private float _bassLevel = 0.18f;
    private float _chordLevel = 0.02f;
    private float _bellLevel = 0.03f;
    private float _leadLevel = 0.15f;

    private volatile float _lowPass = 1.0f;
    private volatile float _delayMix = 0.0f;
    private volatile float _delayFeedback = 0.25f;
    private volatile float _delayTimeMs = 250.0f;
    private volatile int _bitCrushBits = 8;
    private volatile float _bitCrushMix = 1.0f;
    private volatile float _crackleLevel = 1.0f;

    private volatile float _reverbMix = 0.0f;
    private volatile float _chorusMix = 0.0f;

    private volatile float _drumBpm = 95.0f;
    private int _samplesPerBeatDrums;
    private int _drumSamplesSinceBeat;
    private int _drumBeatCounter;
    private int _drumSamplesInBar;
    private int _drumBarCounter;

    // Melody controls
    private volatile float _melodyDensity = 0.70f;
    private volatile float _melodyMutation = 0.20f;
    private volatile bool _melodyUseSeed = true;
    private int _melodySeedLength = 16;
    private int?[] _melodySeed = new int?[]
    {
        0, null, 7, null, 3, null, 10, null,
        7, null, 3, null, 0, null, 7, null
    };
    private int _melodyStepIndex;

    // Melody "cycle" drift state (in semitones) that accumulates over time and is pulled back at seed-loop boundaries.
    private float _melodyCycleDriftState;

    // The current generated melody note for this beat (post-seed + drift/mutation). Other layers should follow THIS.
    private int? _melodyBackboneMidiThisBeat;
    private int? _melodyBackboneSemitoneThisBeat;

    // Bass local step-pattern
    private int _bassPatternLength = 16;
    private int?[] _bassPattern = new int?[]
    {
        0, null, 0, null, 0, null, -2, null,
        0, null, 3, null, 0, null, -2, null
    };
    private int _bassPatternStepIndex;

    // Per-layer melody-follow controls
    private volatile float _chordsMelodyFollow = 0.20f;
    private volatile float _chordsMelodyDrift = 0.05f;
    private volatile float _chordsMelodyMutation = 0.05f;

    private volatile float _bassMelodyFollow = 0.30f;
    private volatile float _bassMelodyDrift = 0.06f;
    private volatile float _bassMelodyMutation = 0.08f;

    private volatile float _padMelodyFollow = 0.40f;
    private volatile float _padMelodyDrift = 0.04f;
    private volatile float _padMelodyMutation = 0.04f;

    private volatile float _bellMelodyFollow = 0.55f;
    private volatile float _bellMelodyDrift = 0.06f;
    private volatile float _bellMelodyMutation = 0.10f;

    // Drift random-walk (semitones)
    private float _chordsDriftState;
    private float _bassDriftState;
    private float _padDriftState;
    private float _bellDriftState;

    // Cached per-beat follow targets
    private int _chordsFollowOffsetThisBeat;
    private int _bassFollowNoteThisBeat;
    private int _padFollowNoteThisBeat;
    private int _bellFollowNoteThisBeat;
    private bool _padFollowActiveThisBeat;
    private bool _bellFollowActiveThisBeat;

    // Melody-driven harmony (used to avoid a fixed 4-chord loop when follow is enabled)
    private readonly int[] _melodyHarmonyChord = new int[3];
    private int _melodyHarmonyHoldBeatsRemaining;
    private int _melodyHarmonyRootSemitone;

    // Melody-driven bass gating (to avoid the always-on bass line pattern)
    private int _bassNoteSamplesRemaining;
    private int _currentBassMidiNote;

    private float _lpState;

    // Chorus (short modulated delay)
    private readonly float[] _chorusBuffer = new float[SampleRate / 2]; // ~0.5s
    private int _chorusWriteIndex;
    private double _chorusLfoPhase;

    // Reverb (simple multi-comb)
    private readonly float[] _reverb1 = new float[673];
    private readonly float[] _reverb2 = new float[853];
    private readonly float[] _reverb3 = new float[991];
    private int _rev1i;
    private int _rev2i;
    private int _rev3i;

    // Delay buffer (mono)
    private readonly float[] _delayBuffer = new float[SampleRate * 2];
    private int _delayWriteIndex;
    private volatile int _delaySamples = (int)(SampleRate * 0.25);

    private void RecomputeDelaySamples(float delayTimeMs)
    {
        int s = (int)(SampleRate * (delayTimeMs / 1000.0f));
        s = Math.Clamp(s, 0, _delayBuffer.Length - 1);
        _delaySamples = s;
    }

    private void RecomputeTiming()
    {
        // Base timing derived from BPM; section variation (_tempoVariation) applies later.
        _samplesPerBeat = (int)(SampleRate * (60.0 / _bpm));
        _samplesPerChord = (int)(SampleRate * (60.0 / _bpm) * 4);

        if (_samplesPerBeat < 1) _samplesPerBeat = 1;
        if (_samplesPerChord < 1) _samplesPerChord = 1;
    }

    private void RecomputeDrumTiming()
    {
        _samplesPerBeatDrums = (int)(SampleRate * (60.0f / _drumBpm));
        if (_samplesPerBeatDrums < 1) _samplesPerBeatDrums = 1;
    }

    private void NormalizeMelodySeed_NoLock()
    {
        _melodySeedLength = Math.Clamp(_melodySeedLength, 4, 64);
        if (_melodySeed == null)
            _melodySeed = Array.Empty<int?>();

        if (_melodySeed.Length != _melodySeedLength)
        {
            var resized = new int?[_melodySeedLength];
            int n = Math.Min(_melodySeed.Length, resized.Length);
            Array.Copy(_melodySeed, resized, n);
            _melodySeed = resized;
        }

        for (int i = 0; i < _melodySeed.Length; i++)
        {
            if (_melodySeed[i].HasValue)
                _melodySeed[i] = Math.Clamp(_melodySeed[i]!.Value, -24, 24);
        }

        _tuning.MelodySeedLength = _melodySeedLength;
        _tuning.MelodySeed = _melodySeed.ToArray();
    }

    private void NormalizeBassPattern_NoLock()
    {
        _bassPatternLength = Math.Clamp(_bassPatternLength, 4, 64);
        if (_bassPattern == null)
            _bassPattern = Array.Empty<int?>();

        if (_bassPattern.Length != _bassPatternLength)
        {
            var resized = new int?[_bassPatternLength];
            int n = Math.Min(_bassPattern.Length, resized.Length);
            Array.Copy(_bassPattern, resized, n);
            _bassPattern = resized;
        }

        for (int i = 0; i < _bassPattern.Length; i++)
        {
            if (_bassPattern[i].HasValue)
                _bassPattern[i] = Math.Clamp(_bassPattern[i]!.Value, -48, 24);
        }

        _tuning.BassPatternLength = _bassPatternLength;
        _tuning.BassPattern = _bassPattern.ToArray();
    }

    public void SetMelodySeedStep(int index, int? semitone)
    {
        lock (_tuningLock)
        {
            NormalizeMelodySeed_NoLock();

            if (index < 0 || index >= _melodySeedLength)
                return;

            if (semitone.HasValue)
                semitone = Math.Clamp(semitone.Value, -24, 24);

            _melodySeed[index] = semitone;
            _tuning.MelodySeed = _melodySeed.ToArray();
        }
    }

    public void SetBassPatternStep(int index, int? semitone)
    {
        lock (_tuningLock)
        {
            NormalizeBassPattern_NoLock();

            if (index < 0 || index >= _bassPatternLength)
                return;

            if (semitone.HasValue)
                semitone = Math.Clamp(semitone.Value, -48, 24);

            _bassPattern[index] = semitone;
            _tuning.BassPattern = _bassPattern.ToArray();
        }
    }

    // Swing rhythm parameters
    private double _swingRatio = 0.6;
    private bool _isSwingBeat = false;

    // Musical variation parameters
    private bool _hiHatActive = true;
    private bool _rideActive = true;
    private bool _padActive = true;
    private bool _bellActive = true;
    private double _tempoVariation = 1.0;
    private int _rootTranspose = 0;
    private int _drumPattern = 0;
    private int _bassPatternMode = 0;
    private double _filterCutoff = 1.0;
    private int _leadMelodyPattern = 0;

    // Perlin-like smooth variation
    private double _smoothVariation = 0;
    private double _variationSpeed = 0.00005;

    // Chord progression library - more variations
    private readonly List<int[][]> _allProgressions = new List<int[][]>();
    private int[][] _activeProgression = Array.Empty<int[]>();
    private double _rootFrequency = 130.81;

    // Drum pattern integration
    private string? _drumStyleFolder;
    private string? _drumPatternPath;
    private sealed record PatternLane(DrumLane Lane, string Steps);
    private sealed record PatternBar(PatternLane[] Lanes, int StepsCount);
    private List<PatternBar>? _drumPatternBars;
    private double _drumPatternSwing = 0.0;

    public LoFiMusicGenerator()
    {
        RecomputeTiming();
        RecomputeDrumTiming();

        // Build extensive progression library
        _allProgressions.Add(new int[][] { new[] { 0, 3, 7 }, new[] { 5, 8, 12 }, new[] { 10, 14, 17 }, new[] { 8, 12, 15 } });
        _allProgressions.Add(new int[][] { new[] { 9, 12, 16 }, new[] { 2, 5, 9 }, new[] { 7, 10, 14 }, new[] { 0, 3, 7 } });
        _allProgressions.Add(new int[][] { new[] { 0, 3, 7 }, new[] { 7, 10, 14 }, new[] { 5, 8, 12 }, new[] { 0, 3, 7 } });
        _allProgressions.Add(new int[][] { new[] { 2, 5, 9 }, new[] { 0, 3, 7 }, new[] { 10, 14, 17 }, new[] { 9, 12, 16 } });
        _allProgressions.Add(new int[][] { new[] { 5, 8, 12 }, new[] { 10, 14, 17 }, new[] { 0, 3, 7 }, new[] { 7, 10, 14 } });
        _allProgressions.Add(new int[][] { new[] { 0, 3, 7 }, new[] { 9, 12, 16 }, new[] { 5, 8, 12 }, new[] { 7, 10, 14 } });
        _allProgressions.Add(new int[][] { new[] { 7, 10, 14 }, new[] { 5, 8, 12 }, new[] { 2, 5, 9 }, new[] { 0, 3, 7 } });
        _allProgressions.Add(new int[][] { new[] { 0, 3, 7 }, new[] { 2, 5, 9 }, new[] { 5, 8, 12 }, new[] { 9, 12, 16 } });

        // Select a drum style folder for the session
        var drumRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "DrumPatterns");
        if (Directory.Exists(drumRoot))
        {
            var styleFolders = Directory.GetDirectories(drumRoot);
            if (styleFolders.Length > 0)
            {
                _drumStyleFolder = styleFolders[_random.Next(styleFolders.Length)];
            }
            else
            {
                _drumStyleFolder = null;
            }
        }
        else
        {
            _drumStyleFolder = null;
        }

        LoadTuningIfPresent();

        SelectNewSection();
    }

    private void SelectNewSection()
    {
        _activeProgression = _allProgressions[_random.Next(_allProgressions.Count)];
        _rootTranspose = new[] { -12, -7, -5, -3, 0, 0, 3, 5, 7, 12 }[_random.Next(10)]; // Full octave range

        _hiHatActive = _random.NextDouble() > 0.15;
        _rideActive = _random.NextDouble() > 0.1;
        _padActive = _random.NextDouble() > 0.25;
        _bellActive = _random.NextDouble() > 0.3;
        _leadMelodyPattern = _random.Next(0, 5);

        _tempoVariation = 0.85 + _random.NextDouble() * 0.30; // 85%-115% - much wider range
        _swingRatio = 0.50 + _random.NextDouble() * 0.20; // 50%-70% swing - much wider variation
        _drumPattern = _random.Next(0, 4);
        _bassPatternMode = _random.Next(0, 3);
        _filterCutoff = 0.7 + _random.NextDouble() * 0.3;
        _variationSpeed = 0.00003 + _random.NextDouble() * 0.00004;

        // Pick a random drum pattern from the chosen style folder
        if (!string.IsNullOrEmpty(_drumStyleFolder))
        {
            var patterns = Directory.GetFiles(_drumStyleFolder, "*.txt");
            if (patterns.Length > 0)
            {
                _drumPatternPath = patterns[_random.Next(patterns.Length)];
                ParseDrumPattern(_drumPatternPath);
            }
        }


        _barsSinceChange = 0;
    }

    private void ParseDrumPattern(string path)
    {
        _drumPatternBars = new List<PatternBar>();
        _drumPatternSwing = 0.0;

        try
        {
            var lines = File.ReadAllLines(path);
            var swingLine = lines.FirstOrDefault(l => l.StartsWith("Swing:", StringComparison.OrdinalIgnoreCase));
            if (swingLine != null)
            {
                var swingVal = swingLine.Split(':')[1].Trim();
                double.TryParse(swingVal, out _drumPatternSwing);
            }

            // Find first pattern block
            int idx = 0;
            while (idx < lines.Length && !lines[idx].StartsWith("[", StringComparison.Ordinal)) idx++;

            while (idx < lines.Length)
            {
                if (lines[idx].StartsWith("["))
                {
                    // Parse this pattern
                    var bar = new List<PatternLane>();
                    idx++;
                    while (idx < lines.Length && !string.IsNullOrWhiteSpace(lines[idx]) && !lines[idx].StartsWith("["))
                    {
                        var raw = lines[idx].Trim();
                        if (raw.Length > 0)
                        {
                            var parts = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                // Token could be "CH" or "CH:" etc.
                                var laneToken = parts[0].Trim().TrimEnd(':');
                                if (Enum.TryParse<DrumLane>(laneToken, ignoreCase: true, out var lane))
                                {
                                    // Pattern is usually the 2nd token
                                    var stepsText = parts[1].Trim();
                                    if (!string.IsNullOrWhiteSpace(stepsText))
                                        bar.Add(new PatternLane(lane, stepsText));
                                }
                            }
                        }
                        idx++;
                    }

                    if (bar.Count > 0)
                    {
                        int steps = bar.Max(x => x.Steps.Length);
                        if (steps > 0)
                            _drumPatternBars.Add(new PatternBar(bar.ToArray(), steps));
                    }
                }
                else
                {
                    idx++;
                }
            }
        }
        catch
        {
            _drumPatternBars = null;
        }
    }


    public WaveOutEvent StartPlayback()
    {
        var waveOut = new WaveOutEvent();
        var provider = new LoFiWaveProvider(this);
        waveOut.Init(provider);
        waveOut.Play();
        return waveOut;
    }

    public IWaveProvider CreateWaveProvider()
    {
        return new LoFiWaveProvider(this);
    }

    public void StopPlayback(WaveOutEvent waveOut)
    {
        try { waveOut?.Stop(); } catch { }
        try { waveOut?.Dispose(); } catch { }
    }

    public void Start()
    {
        using var waveOut = StartPlayback();


        Console.ReadKey(true);
        StopPlayback(waveOut);
    }


    public float GenerateSample()
    {
        // Smooth continuous variation using sine
        _smoothVariation += _variationSpeed;
        double smoothMod = Math.Sin(_smoothVariation) * 0.5 + 0.5;

        _samplesSinceBeat++;
        int adjustedBeatLength = (int)(_samplesPerBeat * _tempoVariation);
        if (_samplesSinceBeat >= adjustedBeatLength)
        {
            _samplesSinceBeat = 0;
            _isSwingBeat = !_isSwingBeat;
        }

        // Independent drum clock
        _drumSamplesSinceBeat++;
        int drumBeatLength = _samplesPerBeatDrums;
        if (_drumSamplesSinceBeat >= drumBeatLength)
        {
            _drumSamplesSinceBeat = 0;
            _drumBeatCounter++;
        }

        int drumBarSamples = Math.Max(1, _samplesPerBeatDrums * 4);
        _drumSamplesInBar++;
        if (_drumSamplesInBar >= drumBarSamples)
        {
            _drumSamplesInBar = 0;
            _drumBarCounter++;
        }


        // --------------------------------------------------------------------
        // MANUAL MODE: solo a chosen drum lane for tuning (mutes musical layers)
        // --------------------------------------------------------------------
        if (_manualMode)
        {
            // Trigger a hit every N beats
            if (_drumSamplesSinceBeat == 0 && (_drumBeatCounter % _manualTriggerEveryBeats == 0))
            {
                _manualHitTotal = drumBeatLength / 2; // short one-shot
                _manualHitRemaining = _manualHitTotal;
            }

            float drum = 0f;
            if (_manualHitRemaining > 0)
            {
                int t = _manualHitTotal - _manualHitRemaining;
                drum = SynthLane(_manualLane, t, _manualHitTotal);
                _manualHitRemaining--;
            }

            float masterManual;
            float drumMasterManual;
            lock (_tuningLock)
            {
                masterManual = _tuning.Master;
                drumMasterManual = _tuning.DrumMaster;
            }

            // Apply per-lane + drum master + master
            if (IsLaneEnabled(_manualLane))
                drum *= GetLaneGain(_manualLane) * drumMasterManual * masterManual;
            else
                drum = 0f;

            // Drum beat counter is advanced above.

            return drum;
        }


        if (_samplesInCurrentChord >= _samplesPerChord)
        {
            _samplesInCurrentChord = 0;
            _currentChordIndex = (_currentChordIndex + 1) % _activeProgression.Length;
            _beatCounter++;

            if (_currentChordIndex == 0)
            {
                _measuresPlayed++;
                _barsSinceChange++;

                // More frequent changes with variable timing
                int changeInterval = _random.Next(1, 3); // Change every 1-2 bars!
                if (_barsSinceChange >= changeInterval && _random.NextDouble() < 0.85) // 85% chance
                {
                    SelectNewSection();
                }
            }
        }
        _samplesInCurrentChord++;

        var currentChord = _activeProgression[_currentChordIndex];
        double transposeFreq = _rootFrequency * Math.Pow(2, _rootTranspose / 12.0);

        // --------------------------------------------------------------------
        // Melody backbone + per-layer follow state (updates once per beat)
        //
        // IMPORTANT: layers follow the CURRENT generated melody note, not the raw seed step.
        // --------------------------------------------------------------------
        if (_samplesSinceBeat == 0)
        {
            int rootMidi = (int)(12 * Math.Log(transposeFreq / 440.0, 2) + 69);
            int chordRootSemitone = (currentChord.Length > 0) ? currentChord[0] : 0;

            // 1) Advance the melody backbone for this beat.
            int? backboneMidi = null;
            bool shouldAttemptNote = _random.NextDouble() < _melodyDensity;

            if (shouldAttemptNote)
            {
                if (_melodyUseSeed)
                {
                    int? seed;
                    int seedLen;
                    lock (_tuningLock)
                    {
                        NormalizeMelodySeed_NoLock();
                        seedLen = _melodySeedLength;
                        seed = seedLen > 0 ? _melodySeed[_melodyStepIndex % seedLen] : null;
                    }

                    // Always advance the seed index even for rests so the cycle has shape.
                    _melodyStepIndex++;

                    if (seed.HasValue)
                    {
                        // Seed steps are semitones from the CURRENT chord root.
                        int baseMidi = (rootMidi + chordRootSemitone) + seed.Value + 12;

                        // Drift: random-walk within bounds derived from MelodyMutation.
                        float maxDrift = 2.0f + 22.0f * Math.Clamp(_melodyMutation, 0.0f, 1.0f); // 2..24
                        if (_random.NextDouble() < _melodyMutation)
                        {
                            // Biased to small steps but allows occasional bigger hops.
                            int driftStep = _random.Next(4) switch
                            {
                                0 => -2,
                                1 => -1,
                                2 => 1,
                                _ => 2
                            };

                            _melodyCycleDriftState = Math.Clamp(_melodyCycleDriftState + driftStep, -maxDrift, maxDrift);
                        }

                        // Extra spice: occasional local mutation around the drifted note.
                        if (_random.NextDouble() < _melodyMutation * 0.35)
                            baseMidi += _random.Next(-2, 3);

                        baseMidi += (int)MathF.Round(_melodyCycleDriftState);
                        backboneMidi = baseMidi;

                        // End-of-seed-cycle pullback so it "loops" into new variations rather than wandering forever.
                        if (seedLen > 0 && (_melodyStepIndex % seedLen) == 0)
                            _melodyCycleDriftState *= 0.5f;
                    }
                }
                else
                {
                    // Legacy non-seed melody mode: still produces a moving backbone.
                    int nextMidi;
                    if (!_leadInitialized)
                        nextMidi = InitLeadPitch(rootMidi + chordRootSemitone);
                    else
                        nextMidi = NextLeadPitch(_currentLeadMidiNote, rootMidi + chordRootSemitone);

                    backboneMidi = nextMidi;
                }
            }

            _melodyBackboneMidiThisBeat = backboneMidi;
            _melodyBackboneSemitoneThisBeat = backboneMidi.HasValue
                ? backboneMidi.Value - (rootMidi + 12)
                : null;

            // Drift random-walk update (in semitones).
            float StepDrift(ref float state, float amount)
            {
                if (amount <= 0.0001f)
                    return state;

                float step = (float)((_random.NextDouble() * 2.0) - 1.0) * (amount * 1.25f);
                state = Math.Clamp(state + step, -7.0f, 7.0f);
                return state;
            }

            int Mutate(int semitone, float mutation)
            {
                if (mutation > 0.0001f && _random.NextDouble() < mutation)
                    semitone += _random.Next(-2, 3);
                return semitone;
            }

            _padFollowActiveThisBeat = false;
            _bellFollowActiveThisBeat = false;

            if (_melodyBackboneSemitoneThisBeat.HasValue)
            {
                int seed = Math.Clamp(_melodyBackboneSemitoneThisBeat.Value, -24, 24);

                _chordsDriftState = StepDrift(ref _chordsDriftState, _chordsMelodyDrift);
                _bassDriftState = StepDrift(ref _bassDriftState, _bassMelodyDrift);
                _padDriftState = StepDrift(ref _padDriftState, _padMelodyDrift);
                _bellDriftState = StepDrift(ref _bellDriftState, _bellMelodyDrift);

                if (_chordsMelodyFollow > 0.0001f && _random.NextDouble() < _chordsMelodyFollow)
                {
                    int target = seed + 12 + (int)MathF.Round(_chordsDriftState);
                    target = Mutate(target, _chordsMelodyMutation);
                    _chordsFollowOffsetThisBeat = Math.Clamp(target, -24, 36);
                }

                if (_bassMelodyFollow > 0.0001f && _random.NextDouble() < _bassMelodyFollow)
                {
                    int target = seed - 12 + (int)MathF.Round(_bassDriftState);
                    target = Mutate(target, _bassMelodyMutation);
                    _bassFollowNoteThisBeat = Math.Clamp(target, -36, 12);
                }

                if (_padMelodyFollow > 0.0001f && _random.NextDouble() < _padMelodyFollow)
                {
                    int target = seed + 12 + (int)MathF.Round(_padDriftState);
                    target = Mutate(target, _padMelodyMutation);
                    _padFollowNoteThisBeat = Math.Clamp(target, -24, 36);
                    _padFollowActiveThisBeat = true;
                }

                if (_bellMelodyFollow > 0.0001f && _random.NextDouble() < _bellMelodyFollow)
                {
                    int target = seed + 24 + (int)MathF.Round(_bellDriftState);
                    target = Mutate(target, _bellMelodyMutation);
                    _bellFollowNoteThisBeat = Math.Clamp(target, -12, 48);
                    _bellFollowActiveThisBeat = true;
                }
            }
        }

        // Chord pad with filter variation
        float chordSample = 0;
        if (_enableChords)
        {
            int[] chordToPlay = currentChord;
            int chordRootToPlay = (currentChord.Length > 0) ? currentChord[0] : 0;

            // If follow is enabled, stop using the static progression loop and instead derive harmony
            // from the CURRENT generated melody note (post mutation/drift).
            if (_chordsMelodyFollow > 0.0001f && _melodyBackboneSemitoneThisBeat.HasValue)
            {
                if (_samplesSinceBeat == 0)
                {
                    if (_melodyHarmonyHoldBeatsRemaining <= 0 || _random.NextDouble() < (_chordsMelodyMutation * 0.35f))
                    {
                        int melodyTone = Math.Clamp(_melodyBackboneSemitoneThisBeat.Value, -24, 24);

                        // Choose a triad flavor; keep it simple but variable.
                        int[] intervals = _random.Next(4) switch
                        {
                            0 => new[] { 0, 3, 7 },  // minor
                            1 => new[] { 0, 4, 7 },  // major
                            2 => new[] { 0, 5, 7 },  // sus4-ish
                            _ => new[] { 0, 2, 7 },  // sus2-ish
                        };

                        // Decide which chord tone the melody is (root/third/fifth).
                        int role = _random.Next(0, 3);
                        int root = melodyTone - intervals[role];

                        // Keep chord roots in a sane range.
                        while (root < -12) root += 12;
                        while (root > 12) root -= 12;

                        _melodyHarmonyRootSemitone = root;
                        _melodyHarmonyChord[0] = root + intervals[0];
                        _melodyHarmonyChord[1] = root + intervals[1];
                        _melodyHarmonyChord[2] = root + intervals[2];

                        _melodyHarmonyHoldBeatsRemaining = 1 + _random.Next(0, 4); // 1..4 beats
                    }

                    _melodyHarmonyHoldBeatsRemaining = Math.Max(0, _melodyHarmonyHoldBeatsRemaining - 1);
                }

                chordToPlay = _melodyHarmonyChord;
                chordRootToPlay = _melodyHarmonyRootSemitone;
            }
            else
            {
                _melodyHarmonyHoldBeatsRemaining = 0;
            }

            if (chordToPlay.Length > 0)
            {
                foreach (var semitone in chordToPlay)
                {
                    double freq = transposeFreq * Math.Pow(2, semitone / 12.0);
                    chordSample += GenerateSineWave(ref _phase, freq, _chordLevel * (float)_filterCutoff);
                }
            }
        }

        // Bass
        float bassSample = 0f;
        if (_enableBass)
        {
            // Mixer behavior:
            // - BassPattern provides the local bassline.
            // - BassMelodyFollow blends toward the current generated melody backbone.
            //   0.0 = 100% pattern, 1.0 = 100% melody-reactive.
            // If the melody is resting this beat, we fall back to the bass pattern.
            if (_samplesSinceBeat == 0)
            {
                int rootMidi = (int)(12 * Math.Log(transposeFreq / 440.0, 2) + 69);
                int chordRootSemitone = (currentChord.Length > 0) ? currentChord[0] : 0;
                if (_chordsMelodyFollow > 0.0001f && _melodyBackboneSemitoneThisBeat.HasValue)
                    chordRootSemitone = _melodyHarmonyRootSemitone;

                // Pattern step (always available for the mixer; legacy patterns are a fallback if no step-pattern is present).
                int? patternStep;
                bool hasStepPattern;
                lock (_tuningLock)
                {
                    NormalizeBassPattern_NoLock();
                    hasStepPattern = _bassPatternLength > 0 && _bassPattern.Length > 0;
                    patternStep = hasStepPattern ? _bassPattern[_bassPatternStepIndex % _bassPatternLength] : null;
                }

                _bassPatternStepIndex++;

                if (hasStepPattern)
                {
                    int? melodySemitone = _melodyBackboneSemitoneThisBeat;

                    // Choose which source to use this beat.
                    double followProb = Math.Clamp(_bassMelodyFollow, 0.0f, 1.0f);
                    bool useMelody = melodySemitone.HasValue && (_random.NextDouble() < followProb);

                    int? chosenSemitone = null;
                    if (useMelody)
                    {
                        int semitone = Math.Clamp(melodySemitone!.Value, -24, 24);
                        semitone -= 12; // put bass under the melody
                        semitone += (int)MathF.Round(_bassDriftState);
                        if (_bassMelodyMutation > 0.0001f && _random.NextDouble() < _bassMelodyMutation)
                            semitone += _random.Next(-2, 3);
                        chosenSemitone = Math.Clamp(semitone, -48, 24);
                    }
                    else if (patternStep.HasValue)
                    {
                        int semitone = patternStep.Value;
                        semitone += (int)MathF.Round(_bassDriftState);
                        if (_bassMelodyMutation > 0.0001f && _random.NextDouble() < _bassMelodyMutation)
                            semitone += _random.Next(-2, 3);
                        chosenSemitone = Math.Clamp(semitone, -48, 24);
                    }

                    if (chosenSemitone.HasValue)
                    {
                        _currentBassMidiNote = (rootMidi + chordRootSemitone + 12) + chosenSemitone.Value;
                        _bassNoteSamplesRemaining = (int)(adjustedBeatLength * (_random.NextDouble() < 0.25 ? 0.5 : 1.0));
                    }
                    else
                    {
                        _bassNoteSamplesRemaining = 0;
                    }
                }
                else
                {
                    // No step-pattern present: fall back to the legacy continuous bass patterns.
                    _bassNoteSamplesRemaining = -1;
                }
            }

            if (_bassNoteSamplesRemaining > 0)
            {
                double freq = 440.0 * Math.Pow(2, (_currentBassMidiNote - 69) / 12.0);
                float env = (float)_bassNoteSamplesRemaining / adjustedBeatLength;
                bassSample = GenerateTriangleWave(ref _bassPhase, freq, _bassLevel * env);
                _bassNoteSamplesRemaining--;
            }
            else if (_bassNoteSamplesRemaining < 0)
            {
                // Legacy bass patterns (kept when melody-follow is disabled).
                int bassNote = (currentChord.Length > 0) ? currentChord[0] : 0;
                double bassMovement = (double)_samplesInCurrentChord / _samplesPerChord;

                switch (_bassPatternMode)
                {
                    case 0: // Walking bass
                        if (currentChord.Length > 1 && bassMovement > 0.5 && bassMovement < 0.6) bassNote = currentChord[1];
                        break;
                    case 1: // Octave jumps
                        if (bassMovement > 0.25 && bassMovement < 0.35) bassNote = bassNote - 12;
                        if (currentChord.Length > 1 && bassMovement > 0.75 && bassMovement < 0.85) bassNote = currentChord[1];
                        break;
                    case 2: // Chromatic approach
                        if (bassMovement > 0.4 && bassMovement < 0.5) bassNote = bassNote + 1;
                        if (currentChord.Length > 2 && bassMovement > 0.6 && bassMovement < 0.7) bassNote = currentChord[2] - 1;
                        break;
                }

                double bassFreq = transposeFreq * Math.Pow(2, bassNote / 12.0) / 2;
                bassSample = GenerateTriangleWave(ref _bassPhase, bassFreq, _bassLevel);
            }
        }

        // Atmospheric pad
        float padSample = 0;
        if (_padActive && _enablePad)
        {
            int padNote = _padFollowActiveThisBeat ? _padFollowNoteThisBeat : (currentChord[1] + 12);
            double padFreq = transposeFreq * Math.Pow(2, padNote / 12.0);
            double detune = 1.005 + smoothMod * 0.005;
            padSample = GenerateSineWave(ref _padPhase, padFreq * detune, _padLevel * 0.08f * (float)smoothMod);

        }

        // Bell melody with varied patterns
        float bellSample = 0;
        if (_bellActive && _enableBell && _samplesInCurrentChord < _samplesPerChord / 8)
        {
            bool shouldPlay = false;
            if (_beatCounter % 3 == 0 || _beatCounter % 5 == 2) shouldPlay = true;
            if (_random.NextDouble() < 0.15) shouldPlay = true; // Random sparkle

            if (shouldPlay)
            {
                int melodicNote;
                if (_bellFollowActiveThisBeat)
                {
                    melodicNote = _bellFollowNoteThisBeat;
                }
                else
                {
                    int noteChoice = _random.Next(currentChord.Length);
                    int octaveShift = _random.NextDouble() < 0.3 ? 24 : 12;
                    melodicNote = currentChord[noteChoice] + octaveShift;
                }

                double bellFreq = transposeFreq * Math.Pow(2, melodicNote / 12.0);
                float decay = (float)Math.Exp(-_samplesInCurrentChord * (0.0002 + _random.NextDouble() * 0.0002));
                bellSample = GenerateSineWave(ref _bellPhase, bellFreq, _bellLevel) * decay;
            }
        }

        // ================= LEAD MELODY =================
        float leadSample = 0f;

        if (_enableLeadMelody)
        {
            // Start/refresh note exactly on beat using the already-advanced melody backbone.
            if (_samplesSinceBeat == 0)
            {
                if (_melodyBackboneMidiThisBeat.HasValue)
                {
                    _currentLeadMidiNote = _melodyBackboneMidiThisBeat.Value;
                    _leadInitialized = true;

                    // Variable sustain (breathing)
                    _leadNoteSamplesRemaining = (int)(adjustedBeatLength * (_random.NextDouble() < 0.3 ? 0.5 : 1.0));
                }
                else
                {
                    _leadNoteSamplesRemaining = 0;
                }
            }

            // Sustain active note
            if (_leadNoteSamplesRemaining > 0)
            {
                double freq = 440.0 * Math.Pow(2, (_currentLeadMidiNote - 69) / 12.0);
                float env = (float)_leadNoteSamplesRemaining / adjustedBeatLength;
                leadSample = GenerateSineWave(ref _lead1Phase, freq, _leadLevel * env * 0.12f);
                _leadNoteSamplesRemaining--;
            }
        }

        // PERCUSSION - Pattern-based or procedural
        int beatInMeasure = (_samplesInCurrentChord / adjustedBeatLength) % 4;
        float drumSample = 0;

        if (_enableDrums)
        {
            if (_drumPatternBars != null && _drumPatternBars.Count > 0)
            {
                // Use pattern-based drums
                drumSample = GeneratePatternDrums();
            }
            else
            {
                // Fallback to procedural drums
                int drumBeatInMeasure = Math.Abs(_drumBeatCounter) % 4;
                drumSample = GenerateProceduralDrums(drumBeatLength, drumBeatInMeasure);
            }

            float drumMaster;
            lock (_tuningLock) drumMaster = _tuning.DrumMaster;
            drumSample *= drumMaster;
        }

        // Vinyl crackle with dynamic intensity
        _crackleIntensity += (_random.NextDouble() - 0.5) * 0.03;
        _crackleIntensity = Math.Max(0, Math.Min(0.12, _crackleIntensity));
        float crackle = (float)(_random.NextDouble() - 0.5) * (float)_crackleIntensity;

        if (!_enableVinylCrackle)
            crackle = 0f;
        else
            crackle *= _crackleLevel;

        if (_random.NextDouble() < 0.0005)
        {
            crackle += (float)(_random.NextDouble() - 0.5) * 0.2f;
        }

        // Wobble
        double wobble = Math.Sin(_noisePhase * 0.00008) * 0.03 + 1.0;
        _noisePhase++;

        float mixed = (chordSample + bassSample + padSample + bellSample + leadSample + drumSample + crackle) * (float)wobble;

        // Lowpass (simple one-pole)
        float lp = _lowPass;
        if (lp < 0.999f)
        {
            // map 0..1 -> 200..8000 Hz
            float cutoffHz = 200.0f + (8000.0f - 200.0f) * lp;
            float alpha = 1.0f - MathF.Exp(-2.0f * MathF.PI * cutoffHz / SampleRate);
            _lpState = _lpState + alpha * (mixed - _lpState);
            mixed = _lpState;
        }

        // Chorus (simple modulated short delay)
        if (_chorusMix > 0.0001f)
        {
            // 15ms base delay, +/- 6ms depth, 0.25Hz LFO
            double lfo = Math.Sin(_chorusLfoPhase);
            _chorusLfoPhase += (2.0 * Math.PI * 0.25) / SampleRate;
            if (_chorusLfoPhase > 2.0 * Math.PI) _chorusLfoPhase -= 2.0 * Math.PI;

            double delayMs = 15.0 + 6.0 * lfo;
            double delaySamples = (SampleRate * delayMs) / 1000.0;
            int di = (int)delaySamples;
            double frac = delaySamples - di;

            int r0 = _chorusWriteIndex - di;
            int r1 = r0 - 1;
            if (r0 < 0) r0 += _chorusBuffer.Length;
            if (r1 < 0) r1 += _chorusBuffer.Length;

            float s0 = _chorusBuffer[r0];
            float s1 = _chorusBuffer[r1];
            float delayed = (float)(s0 * (1.0 - frac) + s1 * frac);

            _chorusBuffer[_chorusWriteIndex] = mixed;
            _chorusWriteIndex++;
            if (_chorusWriteIndex >= _chorusBuffer.Length) _chorusWriteIndex = 0;

            mixed = mixed * (1.0f - _chorusMix) + delayed * _chorusMix;
        }
        else
        {
            _chorusBuffer[_chorusWriteIndex] = mixed;
            _chorusWriteIndex++;
            if (_chorusWriteIndex >= _chorusBuffer.Length) _chorusWriteIndex = 0;
        }

        // Reverb (simple multi-comb)
        if (_reverbMix > 0.0001f)
        {
            const float fb = 0.78f;

            float v1 = _reverb1[_rev1i];
            float v2 = _reverb2[_rev2i];
            float v3 = _reverb3[_rev3i];
            float wet = (v1 + v2 + v3) * 0.35f;

            _reverb1[_rev1i] = mixed + v1 * fb;
            _reverb2[_rev2i] = mixed + v2 * (fb - 0.03f);
            _reverb3[_rev3i] = mixed + v3 * (fb - 0.06f);

            _rev1i++; if (_rev1i >= _reverb1.Length) _rev1i = 0;
            _rev2i++; if (_rev2i >= _reverb2.Length) _rev2i = 0;
            _rev3i++; if (_rev3i >= _reverb3.Length) _rev3i = 0;

            mixed = mixed * (1.0f - _reverbMix) + wet * _reverbMix;
        }

        // Delay
        if (_delayMix > 0.0001f && _delaySamples > 0)
        {
            int readIndex = _delayWriteIndex - _delaySamples;
            if (readIndex < 0) readIndex += _delayBuffer.Length;

            float delayed = _delayBuffer[readIndex];
            float wet = mixed + delayed * _delayMix;

            _delayBuffer[_delayWriteIndex] = mixed + delayed * _delayFeedback;
            _delayWriteIndex++;
            if (_delayWriteIndex >= _delayBuffer.Length) _delayWriteIndex = 0;

            mixed = wet;
        }
        else
        {
            _delayBuffer[_delayWriteIndex] = mixed;
            _delayWriteIndex++;
            if (_delayWriteIndex >= _delayBuffer.Length) _delayWriteIndex = 0;
        }

        // Bitcrush (dry/wet)
        if (_enableBitCrush && _bitCrushMix > 0.0001f)
        {
            float crushed = BitCrush(mixed, _bitCrushBits);
            mixed = mixed * (1.0f - _bitCrushMix) + crushed * _bitCrushMix;
        }

        float master;
        lock (_tuningLock) master = _tuning.Master;

        mixed *= 0.9f * master;
        return Math.Max(-1.0f, Math.Min(1.0f, mixed));
    }

    private float GeneratePatternDrums()
    {
        float drumSample = 0;

        var bars = _drumPatternBars;
        if (bars == null || bars.Count == 0)
            return 0;

        // Cycle bars over time based on drum tempo
        var bar = bars[Math.Abs(_drumBarCounter) % bars.Count];
        if (bar.StepsCount <= 0 || bar.Lanes.Length == 0)
            return 0;

        int steps = bar.StepsCount;
        int drumBarSamples = Math.Max(1, _samplesPerBeatDrums * 4);
        int step = (int)((_drumSamplesInBar / (float)drumBarSamples) * steps) % steps;

        // Render the hit across the whole step so tails are audible.
        int samplesPerStep = Math.Max(1, drumBarSamples / steps);
        int sampleInStep = _drumSamplesInBar % samplesPerStep;

        foreach (var lane in bar.Lanes)
        {
            if (step >= lane.Steps.Length)
                continue;

            char hit = lane.Steps[step];
            if (hit != 'X' && hit != 'x')
                continue;

            // Sub-layer gating
            if ((lane.Lane == DrumLane.CH || lane.Lane == DrumLane.OH) && !_enableHiHat)
                continue;
            if (lane.Lane == DrumLane.CY && !_enableRide)
                continue;

            if (!IsLaneEnabled(lane.Lane))
                continue;

            float g = GetLaneGain(lane.Lane);
            // Use the lane synth so *all* lanes can contribute to audible patterns.
            drumSample += SynthLane(lane.Lane, sampleInStep, samplesPerStep) * g;
        }

        return drumSample;
    }

    // ============================================================================
    // Simple per-lane drum synthesis (for manual mode and as building blocks)
    // Not fancy yet, but each lane is distinct enough to verify availability.
    // ============================================================================
    private float SynthLane(DrumLane lane, int t, int total)
    {
        // t: samples since hit start; total: total samples in this one-shot
        float x = 0f;
        float envFast = (float)Math.Exp(-t * 0.025);
        float envMed = (float)Math.Exp(-t * 0.012);
        float envSlow = (float)Math.Exp(-t * 0.004);

        // mild grit
        float noise = (float)(_random.NextDouble() - 0.5);

        switch (lane)
        {
            case DrumLane.BD: // kick
                              // low thump + tiny click
                x = (float)Math.Sin(2.0 * Math.PI * (70.0 - 40.0 * (t / (float)Math.Max(1, total))) * t / SampleRate) * envMed * 1.2f
                    + noise * envFast * 0.08f;
                break;

            case DrumLane.SD: // snare
                x = noise * envMed * 0.9f
                    + (float)Math.Sin(2.0 * Math.PI * 190.0 * t / SampleRate) * envFast * 0.25f;
                break;

            case DrumLane.CH: // closed hat
                x = noise * envFast * 0.55f;
                break;

            case DrumLane.OH: // open hat
                x = noise * envMed * 0.65f;
                break;

            case DrumLane.CY: // cymbal
                x = noise * envSlow * 0.50f;
                break;

            case DrumLane.CB: // cowbell-ish / bell
                x = (float)Math.Sin(2.0 * Math.PI * 540.0 * t / SampleRate) * envMed * 0.55f
                    + (float)Math.Sin(2.0 * Math.PI * 810.0 * t / SampleRate) * envFast * 0.25f;
                break;

            case DrumLane.CP: // clap
                              // short multi-burst
                float e1 = (float)Math.Exp(-Math.Max(0, t - 0) * 0.060);
                float e2 = (float)Math.Exp(-Math.Max(0, t - 200) * 0.060);
                float e3 = (float)Math.Exp(-Math.Max(0, t - 450) * 0.060);
                float env = Math.Max(e1, Math.Max(e2, e3));
                x = noise * env * 0.75f;
                break;

            case DrumLane.RS: // rimshot
                x = (float)Math.Sin(2.0 * Math.PI * 2100.0 * t / SampleRate) * envFast * 0.5f
                    + noise * envFast * 0.25f;
                break;

            case DrumLane.HT: // high tom
                x = (float)Math.Sin(2.0 * Math.PI * 240.0 * t / SampleRate) * envMed * 0.9f;
                break;

            case DrumLane.MT: // mid tom
                x = (float)Math.Sin(2.0 * Math.PI * 170.0 * t / SampleRate) * envMed * 0.9f;
                break;

            case DrumLane.LT: // low tom
                x = (float)Math.Sin(2.0 * Math.PI * 120.0 * t / SampleRate) * envMed * 1.0f;
                break;

            case DrumLane.AC: // accent (short metallic tick)
                x = (float)Math.Sin(2.0 * Math.PI * 3500.0 * t / SampleRate) * envFast * 0.35f
                    + noise * envFast * 0.20f;
                break;

            case DrumLane.GH: // ghost hat / ghost perc
                x = noise * envFast * 0.22f;
                break;
        }

        // light soft-clip dirt
        x = (float)Math.Tanh(x * 1.6f);
        return x;
    }



    private float GenerateProceduralDrums(int adjustedBeatLength, int beatInMeasure)
    {
        float drumSample = 0;

        bool drumSwingBeat = (_drumBeatCounter % 2) != 0;

        // Ride cymbal
        if (_rideActive && _enableRide)
        {
            int rideInterval = drumSwingBeat ?
                (int)(adjustedBeatLength * _swingRatio) :
                (int)(adjustedBeatLength * (1 - _swingRatio));

            if (_drumSamplesSinceBeat < rideInterval / 20)
            {
                float noise = (float)(_random.NextDouble() - 0.5);
                float tone = (float)Math.Sin(_drumSamplesSinceBeat * 0.3) * 0.3f;
                if (IsLaneEnabled(DrumLane.CY))
                    drumSample += (noise * 0.4f + tone) * 0.62f * GetLaneGain(DrumLane.CY);
            }
        }

        // Hi-hat with patterns
        if (_hiHatActive && _enableHiHat)
        {
            bool playHiHat = false;
            switch (_drumPattern)
            {
                case 0: playHiHat = _drumSamplesSinceBeat > adjustedBeatLength / 2; break;
                case 1: playHiHat = _drumBeatCounter % 2 == 0 && _drumSamplesSinceBeat > adjustedBeatLength / 2; break;
                case 2: playHiHat = true; break;
                case 3: playHiHat = _random.NextDouble() < 0.6; break;
            }

            if (playHiHat && _drumSamplesSinceBeat < adjustedBeatLength / 30)
            {
                float noise = (float)(_random.NextDouble() - 0.5);
                if (IsLaneEnabled(DrumLane.CH))
                    drumSample += noise * 0.68f * (float)Math.Exp(-_drumSamplesSinceBeat * 0.01) * GetLaneGain(DrumLane.CH);
            }
        }

        // Kick with variations
        bool kickHit = false;
        switch (_drumPattern)
        {
            case 0: kickHit = beatInMeasure == 0 || beatInMeasure == 2; break;
            case 1: kickHit = beatInMeasure == 0 || beatInMeasure == 3; break;
            case 2: kickHit = beatInMeasure == 0 || (beatInMeasure == 2 && _random.NextDouble() < 0.6); break;
            case 3: kickHit = beatInMeasure == 0; break;
        }

        if (kickHit && _drumSamplesSinceBeat < adjustedBeatLength / 15 && IsLaneEnabled(DrumLane.BD))
        {
            float kickFreq = 60f - (_drumSamplesSinceBeat * 2f);
            drumSample += (float)Math.Sin(_drumSamplesSinceBeat * kickFreq * 0.01) * 0.95f *
                         (float)Math.Exp(-_drumSamplesSinceBeat * 0.015) * GetLaneGain(DrumLane.BD);
        }

        // Snare
        if ((beatInMeasure == 1 || beatInMeasure == 3) && _drumSamplesSinceBeat < adjustedBeatLength / 12 && IsLaneEnabled(DrumLane.SD))
        {
            float noise = (float)(_random.NextDouble() - 0.5);
            float tone = (float)Math.Sin(_drumSamplesSinceBeat * 0.2);
            drumSample += (noise * 0.5f + tone * 0.3f) * 0.25f *
                         (float)Math.Exp(-_drumSamplesSinceBeat * 0.012) * GetLaneGain(DrumLane.SD);
        }

        return drumSample;
    }

    private float GenerateSineWave(ref double phase, double frequency, float amplitude)
    {
        float sample = (float)(Math.Sin(phase) * amplitude);
        phase += 2 * Math.PI * frequency / SampleRate;
        if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
        return sample;
    }

    private float GenerateTriangleWave(ref double phase, double frequency, float amplitude)
    {
        float sample = (float)((2.0 * Math.Abs(2.0 * (phase / (2 * Math.PI) -
            Math.Floor(phase / (2 * Math.PI) + 0.5))) - 1.0) * amplitude);
        phase += 2 * Math.PI * frequency / SampleRate;
        if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
        return sample;
    }

    private float BitCrush(float sample, int bits)
    {
        int levels = (int)Math.Pow(2, bits);
        return (float)Math.Round(sample * levels) / levels;
    }

    private int InitLeadPitch(int rootMidi)
    {
        int[] dorian = { 0, 2, 3, 5, 7, 9, 10 };
        int degree = dorian[_random.Next(dorian.Length)];
        return rootMidi + degree + 24 + (_random.Next(0, 2) * 12);
    }

    private int NextLeadPitch(int current, int rootMidi)
    {
        int roll = _random.Next(100);
        int interval =
            roll < 70 ? _random.Next(-2, 3) :
            roll < 90 ? _random.Next(-5, 6) :
                        _random.Next(-12, 13);

        int target = current + interval;

        // Bebop chromatic approach
        if (_random.NextDouble() < 0.3)
            target += (target > current) ? -1 : 1;

        return Math.Clamp(target, rootMidi + 24, rootMidi + 48);
    }

    private class LoFiWaveProvider : IWaveProvider
    {
        private readonly LoFiMusicGenerator _generator;

        public LoFiWaveProvider(LoFiMusicGenerator generator)
        {
            _generator = generator;
            WaveFormat = new WaveFormat(SampleRate, 16, 1);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i += 2)
            {
                float sample = _generator.GenerateSample();
                short sampleValue = (short)(sample * short.MaxValue);

                buffer[offset + i] = (byte)(sampleValue & 0xFF);
                buffer[offset + i + 1] = (byte)((sampleValue >> 8) & 0xFF);
            }
            return count;
        }
    }
}

}
