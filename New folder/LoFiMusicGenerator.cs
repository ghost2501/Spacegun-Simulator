using Microsoft.VisualBasic;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spacegun_Simulator
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
    private double _lead2Phase = 0;
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

    public sealed class AudioTuningSettings
    {
        public float PadLevel { get; set; } = 0.06f;
        public float BassLevel { get; set; } = 0.18f;
        public float LeadLevel { get; set; } = 0.25f;

        public float DrumMaster { get; set; } = 1.0f;
        public float Master { get; set; } = 0.50f;

        // Per-lane gain multipliers (1.0 = default)
        public Dictionary<string, float> DrumGains { get; set; } = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { "CH", 0.7f }, { "OH", 0.5f }, { "CY", 0.3f }, { "CB", 0.4f }, { "CP", 0.3f }, { "RS", 0.5f },
        { "HT", 0.7f }, { "MT", 0.7f }, { "LT", 0.9f }, { "SD", 0.25f }, { "BD", 0.5f }, { "AC", 0.2f }, { "GH", 1.0f },
    };
    }

    private void LoadTuningIfPresent()
    {
        try
        {
            if (!File.Exists(TuningPath)) return;
            var json = File.ReadAllText(TuningPath);
            var loaded = JsonSerializer.Deserialize<AudioTuningSettings>(json);
            if (loaded == null) return;

            lock (_tuningLock)
            {
                _tuning = loaded;
                _padLevel = _tuning.PadLevel;
                _bassLevel = _tuning.BassLevel;
                _leadLevel = _tuning.LeadLevel;
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
                _tuning.PadLevel = _padLevel;
                _tuning.BassLevel = _bassLevel;
                _tuning.LeadLevel = _leadLevel;
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
                case "Pad": _padLevel = Math.Clamp(_padLevel + delta, 0.0f, 1f); break;
                case "Bass": _bassLevel = Math.Clamp(_bassLevel + delta, 0.0f, 1.0f); break;
                case "Lead": _leadLevel = Math.Clamp(_leadLevel + delta, 0.0f, 0.5f); break;
                case "DrumMaster": _tuning.DrumMaster = Math.Clamp(_tuning.DrumMaster + delta, 0.0f, 4.0f); break;
                case "Master": _tuning.Master = Math.Clamp(_tuning.Master + delta, 0.0f, 4.0f); break;
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
                PadLevel = _padLevel,
                BassLevel = _bassLevel,
                LeadLevel = _leadLevel,
                DrumMaster = _tuning.DrumMaster,
                Master = _tuning.Master,
                DrumGains = new Dictionary<string, float>(_tuning.DrumGains, StringComparer.OrdinalIgnoreCase)
            };
        }
    }


    private float _padLevel = 0.12f;
    private float _bassLevel = 0.18f;
    private float _chordLevel = 0.02f;
    private float _leadLevel = 0.15f;
    private float _drumBoost = 1.35f;

    // ================= INPUT =================
    private volatile bool _showMixer = false;

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
    private int _bassPattern = 0;
    private double _filterCutoff = 1.0;
    private bool _leadMelodyActive = false;
    private int _leadMelodyPattern = 0;

    // Perlin-like smooth variation
    private double _smoothVariation = 0;
    private double _variationSpeed = 0.00005;

    // Chord progression library - more variations
    private readonly List<int[][]> _allProgressions = new List<int[][]>();
    private int[][] _activeProgression;
    private double _rootFrequency = 130.81;

    // Drum pattern integration
    private string _drumStyleFolder;
    private string _drumPatternPath;
    private List<string[]> _drumPatternSteps; // Each string[] is a bar, each char is a step
    private int _drumPatternIndex = 0;
    private double _drumPatternSwing = 0.0;

    public LoFiMusicGenerator()
    {
        _samplesPerChord = (int)(SampleRate * (60.0 / _bpm) * 4);
        _samplesPerBeat = (int)(SampleRate * (60.0 / _bpm));

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
        var drumRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "DrumPatterns");
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
        _leadMelodyActive = _random.NextDouble() > 0.4; // Much more frequent
        _leadMelodyPattern = _random.Next(0, 5);

        _tempoVariation = 0.85 + _random.NextDouble() * 0.30; // 85%-115% - much wider range
        _swingRatio = 0.50 + _random.NextDouble() * 0.20; // 50%-70% swing - much wider variation
        _drumPattern = _random.Next(0, 4);
        _bassPattern = _random.Next(0, 3);
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
        _drumPatternSteps = new List<string[]>();
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
                    var bar = new List<string>();
                    idx++;
                    while (idx < lines.Length && !string.IsNullOrWhiteSpace(lines[idx]) && !lines[idx].StartsWith("["))
                    {
                        if (lines[idx].Contains("BD") || lines[idx].Contains("SD") || lines[idx].Contains("CH") || lines[idx].Contains("OH"))
                        {
                            var parts = lines[idx].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1)
                                bar.Add(parts[1].Trim());
                        }
                        idx++;
                    }
                    if (bar.Count > 0)
                        _drumPatternSteps.Add(bar.ToArray());
                }
                else
                {
                    idx++;
                }
            }
            _drumPatternIndex = 0;
        }
        catch (Exception ex)
        {
            _drumPatternSteps = null;
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


        // --------------------------------------------------------------------
        // MANUAL MODE: solo a chosen drum lane for tuning (mutes musical layers)
        // --------------------------------------------------------------------
        if (_manualMode)
        {
            // Trigger a hit every N beats
            if (_samplesSinceBeat == 0 && (_beatCounter % _manualTriggerEveryBeats == 0))
            {
                _manualHitTotal = adjustedBeatLength / 2; // short one-shot
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
            drum *= GetLaneGain(_manualLane) * drumMasterManual * masterManual;

            // Advance beat counter at beat boundary (so manual triggering is stable)
            if (_samplesSinceBeat == 0) _beatCounter++;

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

        // Chord pad with filter variation
        float chordSample = 0;
        foreach (var semitone in currentChord)
        {
            double freq = transposeFreq * Math.Pow(2, semitone / 12.0);

            chordSample += GenerateSineWave(ref _phase, freq, _chordLevel * (float)_filterCutoff);
        }

        // Bass patterns
        int bassNote = currentChord[0];
        double bassMovement = (double)_samplesInCurrentChord / _samplesPerChord;

        switch (_bassPattern)
        {
            case 0: // Walking bass
                if (bassMovement > 0.5 && bassMovement < 0.6) bassNote = currentChord[1];
                break;
            case 1: // Octave jumps
                if (bassMovement > 0.25 && bassMovement < 0.35) bassNote = currentChord[0] - 12;
                if (bassMovement > 0.75 && bassMovement < 0.85) bassNote = currentChord[1];
                break;
            case 2: // Chromatic approach
                if (bassMovement > 0.4 && bassMovement < 0.5) bassNote = currentChord[0] + 1;
                if (bassMovement > 0.6 && bassMovement < 0.7) bassNote = currentChord[2] - 1;
                break;
        }

        double bassFreq = transposeFreq * Math.Pow(2, bassNote / 12.0) / 2;
        float bassSample = GenerateTriangleWave(ref _bassPhase, bassFreq, 0.18f);

        // Atmospheric pad
        float padSample = 0;
        if (_padActive)
        {
            double padFreq = transposeFreq * Math.Pow(2, (currentChord[1] + 12) / 12.0);
            double detune = 1.005 + smoothMod * 0.005;
            padSample = GenerateSineWave(ref _padPhase, padFreq * detune, _padLevel * 0.08f * (float)smoothMod);

        }

        // Bell melody with varied patterns
        float bellSample = 0;
        if (_bellActive && _samplesInCurrentChord < _samplesPerChord / 8)
        {
            bool shouldPlay = false;
            if (_beatCounter % 3 == 0 || _beatCounter % 5 == 2) shouldPlay = true;
            if (_random.NextDouble() < 0.15) shouldPlay = true; // Random sparkle

            if (shouldPlay)
            {
                int noteChoice = _random.Next(currentChord.Length);
                int octaveShift = _random.NextDouble() < 0.3 ? 24 : 12;
                int melodicNote = currentChord[noteChoice] + octaveShift;

                double bellFreq = transposeFreq * Math.Pow(2, melodicNote / 12.0);
                float decay = (float)Math.Exp(-_samplesInCurrentChord * (0.0002 + _random.NextDouble() * 0.0002));
                bellSample = GenerateSineWave(ref _bellPhase, bellFreq, 0.03f) * decay;
            }
        }

        // ================= LEAD MELODY =================
        float leadSample = 0f;

        if (_leadMelodyActive)
        {
            // Start new note exactly on beat if none active
            if (_leadNoteSamplesRemaining <= 0 && _samplesSinceBeat == 0)
            {
                if (_random.NextDouble() < 0.7) // reliable but not constant
                {
                    int rootMidi = (int)(12 * Math.Log(transposeFreq / 440.0, 2) + 69);

                    if (!_leadInitialized)
                    {
                        _currentLeadMidiNote = InitLeadPitch(rootMidi);
                        _leadInitialized = true;
                    }
                    else
                    {
                        _currentLeadMidiNote = NextLeadPitch(_currentLeadMidiNote, rootMidi);
                    }

                    // Variable sustain (breathing)
                    _leadNoteSamplesRemaining = (int)(adjustedBeatLength * (_random.NextDouble() < 0.3 ? 0.5 : 1.0));
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

        if (_drumPatternSteps != null && _drumPatternSteps.Count > 0)
        {
            // Use pattern-based drums
            drumSample = GeneratePatternDrums(adjustedBeatLength);
        }
        else
        {
            // Fallback to procedural drums
            drumSample = GenerateProceduralDrums(adjustedBeatLength, beatInMeasure);
        }

        // Vinyl crackle with dynamic intensity
        _crackleIntensity += (_random.NextDouble() - 0.5) * 0.03;
        _crackleIntensity = Math.Max(0, Math.Min(0.12, _crackleIntensity));
        float crackle = (float)(_random.NextDouble() - 0.5) * (float)_crackleIntensity;

        if (_random.NextDouble() < 0.0005)
        {
            crackle += (float)(_random.NextDouble() - 0.5) * 0.2f;
        }

        // Wobble
        double wobble = Math.Sin(_noisePhase * 0.00008) * 0.03 + 1.0;
        _noisePhase++;

        float mixed = (chordSample + bassSample + padSample + bellSample + leadSample + drumSample + crackle) * (float)wobble;
        mixed = BitCrush(mixed, 8);

        float master;
        lock (_tuningLock) master = _tuning.Master;

        mixed *= 0.9f * master;
        return Math.Max(-1.0f, Math.Min(1.0f, mixed));
    }

    private float GeneratePatternDrums(int adjustedBeatLength)
    {
        float drumSample = 0;

        // Use the first bar for now (expand to cycle bars if desired)
        var bar = _drumPatternSteps[0];
        int steps = bar[0].Length; // All lines should be same length
        int step = (int)((_samplesInCurrentChord / (float)_samplesPerChord) * steps) % steps;

        // Check if we're at the beginning of this step
        int samplesPerStep = _samplesPerChord / steps;
        int sampleInStep = _samplesInCurrentChord % samplesPerStep;

        // Only trigger on step start
        if (sampleInStep < samplesPerStep / 20) // Trigger window
        {
            // Map pattern lines to sounds
            for (int i = 0; i < bar.Length; i++)
            {
                string line = bar[i];
                if (step < line.Length)
                {
                    char hit = line[step];
                    if (hit == 'X' || hit == 'x')
                    {
                        // Map i to instrument: 0=CH, 1=OH, 2=SD, 3=BD (adjust as needed)
                        switch (i)
                        {
                            case 0: // CH (Closed Hi-Hat)
                                drumSample += (float)(_random.NextDouble() - 0.5) * 0.5f *
                                             (float)Math.Exp(-sampleInStep * 0.01);
                                break;
                            case 1: // OH (Open Hi-Hat)
                                drumSample += (float)(_random.NextDouble() - 0.5) * 0.7f *
                                             (float)Math.Exp(-sampleInStep * 0.005);
                                break;
                            case 2: // SD (Snare)
                                float noise = (float)(_random.NextDouble() - 0.5);
                                float tone = (float)Math.Sin(sampleInStep * 0.2);
                                drumSample += (noise * 0.5f + tone * 0.3f) * 0.8f *
                                             (float)Math.Exp(-sampleInStep * 0.012);
                                break;
                            case 3: // BD (Bass Drum)
                                float kickFreq = 60f - (sampleInStep * 2f);
                                drumSample += (float)Math.Sin(sampleInStep * kickFreq * 0.01) * 1.0f *
                                             (float)Math.Exp(-sampleInStep * 0.015);
                                break;
                        }
                    }
                }
            }
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

        // Ride cymbal
        if (_rideActive)
        {
            int rideInterval = _isSwingBeat ?
                (int)(adjustedBeatLength * _swingRatio) :
                (int)(adjustedBeatLength * (1 - _swingRatio));

            if (_samplesSinceBeat < rideInterval / 20)
            {
                float noise = (float)(_random.NextDouble() - 0.5);
                float tone = (float)Math.Sin(_samplesSinceBeat * 0.3) * 0.3f;
                drumSample += (noise * 0.4f + tone) * 0.62f;
            }
        }

        // Hi-hat with patterns
        if (_hiHatActive)
        {
            bool playHiHat = false;
            switch (_drumPattern)
            {
                case 0: playHiHat = _samplesSinceBeat > adjustedBeatLength / 2; break;
                case 1: playHiHat = _beatCounter % 2 == 0 && _samplesSinceBeat > adjustedBeatLength / 2; break;
                case 2: playHiHat = true; break;
                case 3: playHiHat = _random.NextDouble() < 0.6; break;
            }

            if (playHiHat && _samplesSinceBeat < adjustedBeatLength / 30)
            {
                float noise = (float)(_random.NextDouble() - 0.5);
                drumSample += noise * 0.68f * (float)Math.Exp(-_samplesSinceBeat * 0.01);
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

        if (kickHit && _samplesSinceBeat < adjustedBeatLength / 15)
        {
            float kickFreq = 60f - (_samplesSinceBeat * 2f);
            drumSample += (float)Math.Sin(_samplesSinceBeat * kickFreq * 0.01) * 0.95f *
                         (float)Math.Exp(-_samplesSinceBeat * 0.015);
        }

        // Snare
        if ((beatInMeasure == 1 || beatInMeasure == 3) && _samplesSinceBeat < adjustedBeatLength / 12)
        {
            float noise = (float)(_random.NextDouble() - 0.5);
            float tone = (float)Math.Sin(_samplesSinceBeat * 0.2);
            drumSample += (noise * 0.5f + tone * 0.3f) * 0.25f *
                         (float)Math.Exp(-_samplesSinceBeat * 0.612);
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
