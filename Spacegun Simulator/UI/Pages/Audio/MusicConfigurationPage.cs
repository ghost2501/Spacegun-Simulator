using Spacegun_Simulator.Audio;
using Spacegun_Simulator;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.Audio;

public sealed class MusicConfigurationPage : PageBase
{
    public override string Id => PageId.MusicConfiguration;
    public override string Title => "MUSIC CONFIGURATION";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true,
		FooterHint: "Edit(↩) Space=ON/OFF (S)ave (L)oad (B)ack"
    );

    private enum Mode
    {
        List,
        Edit,
        MelodySeed,
        BassPattern,
        SavePreset,
        LoadPreset
    }

    private sealed record Item(
        string Id,
        string Label,
        ItemKind Kind,
        string[] Effects
    );

    private enum ItemKind
    {
        Layer,
        DrumLane
    }

    private static readonly Item[] Items =
    [
        new("layer:LeadMelody", "Lead Melody Generator", ItemKind.Layer, ["LowPass (global)", "Delay (global)", "BitCrush (global)"]),

        new("layer:Chords", "Chords (Sine Pad)", ItemKind.Layer, ["LowPass (global)", "Delay (global)", "BitCrush (global)"]),
        new("layer:Bass", "Bass (Triangle)", ItemKind.Layer, ["LowPass (global)", "Delay (global)", "BitCrush (global)"]),
        new("layer:Pad", "Atmospheric Pad", ItemKind.Layer, ["LowPass (global)", "Delay (global)", "BitCrush (global)"]),
        new("layer:Bell", "Bell Melody", ItemKind.Layer, ["LowPass (global)", "Delay (global)", "BitCrush (global)"]),
        new("layer:Drums", "Drums", ItemKind.Layer, ["Pattern / procedural", "LowPass (global)", "Delay (global)"]),
        new("layer:HiHat", "  - Hi-Hat (sub-layer)", ItemKind.Layer, ["Procedural + pattern gating"]),
        new("layer:Ride", "  - Ride (sub-layer)", ItemKind.Layer, ["Procedural gating"]),
        new("layer:VinylCrackle", "Vinyl Crackle", ItemKind.Layer, ["Level", "Random pops + intensity drift"]),
        new("layer:BitCrush", "BitCrush", ItemKind.Layer, ["Bits", "Mix"]),

        new("lane:BD", "Drum Lane: BD (Kick)", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:SD", "Drum Lane: SD (Snare)", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:CH", "Drum Lane: CH (Closed Hat)", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:OH", "Drum Lane: OH (Open Hat)", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:CY", "Drum Lane: CY (Cymbal/Ride)", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:CB", "Drum Lane: CB", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:CP", "Drum Lane: CP", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:RS", "Drum Lane: RS", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:HT", "Drum Lane: HT", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:MT", "Drum Lane: MT", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:LT", "Drum Lane: LT", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:AC", "Drum Lane: AC", ItemKind.DrumLane, ["Gain (tuning)"]),
        new("lane:GH", "Drum Lane: GH", ItemKind.DrumLane, ["Gain (tuning)"]),
    ];

    private Mode _mode;
    private int _selected;
    private int _scroll;

    private int _editParamIndex;
    private int _editParamScroll;

    private int _seedIndex;
    private int _bassSeedIndex;

    private string _presetNameInput = string.Empty;
    private string _statusMessage = string.Empty;

    private string[] _presetNames = Array.Empty<string>();
    private int _presetSelected;
    private int _presetScroll;

    public override void OnEnter(UiContext ui)
    {
        _mode = Mode.List;
        _selected = 0;
        _scroll = 0;
        _statusMessage = string.Empty;
    }

	private int GetListRowCount()
		=> Items.Length + 1; // +1 for Master Volume

	private bool IsMasterRowSelected()
		=> _selected == 0;

	private Item GetSelectedItem()
		=> Items[Math.Clamp(_selected - 1, 0, Items.Length - 1)];

    protected override void RenderBody(UiContext ui)
    {
        switch (_mode)
        {
            case Mode.List:
                RenderList(ui);
                break;
            case Mode.Edit:
                RenderEdit(ui);
                break;
            case Mode.MelodySeed:
                RenderMelodySeed(ui);
                break;
            case Mode.BassPattern:
                RenderBassPattern(ui);
                break;
            case Mode.SavePreset:
                RenderSavePreset(ui);
                break;
            case Mode.LoadPreset:
                RenderLoadPreset(ui);
                break;
        }
    }

    private void RenderList(UiContext ui)
    {
        ui.WriteLine("Toggle layers/lanes to mix generator streams.");
        ui.WriteLine("Enter opens sliders for the selected item.");
        ui.WriteLine("Tip: Edit Lead Melody Generator, then press E for Seed Editor.");
        ui.WriteLine();

        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            ui.WriteLine(_statusMessage);
            ui.WriteLine();
        }

        int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight - 4 : 14;
        viewport = Math.Max(8, viewport);

        int rows = GetListRowCount();
        int maxScroll = Math.Max(0, rows - viewport);
        _scroll = Math.Clamp(_scroll, 0, maxScroll);

        if (_selected < _scroll) _scroll = _selected;
        if (_selected >= _scroll + viewport) _scroll = _selected - viewport + 1;

        int end = Math.Min(rows, _scroll + viewport);
        var snapshot = PageMusicSystem.GetTuningSnapshot();
        for (int row = _scroll; row < end; row++)
        {
			if (row == 0)
			{
				string cursor = (row == _selected) ? ">" : " ";
                float master = Math.Clamp(snapshot.Master, 0.0f, 4.0f);
                int pct = (int)MathF.Round(master / 4.0f * 100.0f);
                string bar = RenderBar(master, 0.0f, 4.0f, 12);
                ui.WriteLine($"{cursor} Master Volume {bar} {pct}%");
				continue;
			}

			var item = Items[row - 1];
			bool enabled = TryGetEnabled(item);

			string cursorItem = (row == _selected) ? ">" : " ";
			string mark = enabled ? "[x]" : "[ ]";
			ui.WriteLine($"{cursorItem} {mark} {item.Label}");
        }
    }

    private sealed record ParamDef(
        string Label,
        float Min,
        float Max,
        Func<LoFiMusicGenerator.AudioTuningSettings, float> Get,
        Action<float> Adjust,
        float FineStep,
        float CoarseStep,
        bool IsInteger = false,
        bool IsPlaceholder = false
    );

    private void RenderEdit(UiContext ui)
    {
        var snapshot = PageMusicSystem.GetTuningSnapshot();
        ParamDef[] parameters;

        if (IsMasterRowSelected())
        {
            ui.WriteLine("MASTER VOLUME");
            ui.WriteLine();
            ui.WriteLine("Sliders:");
            ui.WriteLine();
            parameters = BuildMasterParams(snapshot);
        }
        else
        {
            var item = GetSelectedItem();
            bool enabled = TryGetEnabled(item);

            parameters = BuildParams(item, snapshot);

            ui.WriteLine(item.Label);
            ui.WriteLine();
            ui.WriteLine($"Enabled: {(enabled ? "ON" : "OFF")} (Space to toggle)");
            ui.WriteLine();
            ui.WriteLine("Sliders:");
            ui.WriteLine();
        }

        // Each parameter consumes 2 lines (label + bar/value), so viewport must be computed in PARAMS, not lines.
        int viewportLines = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight - 8 : 10;
        viewportLines = Math.Max(6, viewportLines);
        int viewport = Math.Max(3, viewportLines / 2);

        int maxScroll = Math.Max(0, parameters.Length - viewport);
        _editParamScroll = Math.Clamp(_editParamScroll, 0, maxScroll);
        _editParamIndex = Math.Clamp(_editParamIndex, 0, Math.Max(0, parameters.Length - 1));

        if (_editParamIndex < _editParamScroll) _editParamScroll = _editParamIndex;
        if (_editParamIndex >= _editParamScroll + viewport) _editParamScroll = _editParamIndex - viewport + 1;

        int end = Math.Min(parameters.Length, _editParamScroll + viewport);
        for (int i = _editParamScroll; i < end; i++)
        {
            var p = parameters[i];
            float value = p.Get(snapshot);
            string cursor = (i == _editParamIndex) ? ">" : " ";
            string bar = RenderBar(value, p.Min, p.Max, 18);

            string valText = p.IsInteger
                ? ((int)MathF.Round(value)).ToString()
                : value.ToString("0.###");

            string placeholder = p.IsPlaceholder ? " (placeholder)" : string.Empty;
            ui.WriteLine($"{cursor} {p.Label}{placeholder}");
            ui.WriteLine($"    {bar}  {valText}");
        }

        ui.WriteLine();
        ui.WriteLine("Up/Down=Select  Left/Right=Adjust  PgUp/PgDn=Coarse");
        ui.WriteLine("B=Back  S=Save Preset  L=Load Preset");

		if (!IsMasterRowSelected())
		{
			var item = GetSelectedItem();
			if (item.Kind == ItemKind.Layer && ParseLayer(item.Id) == LoFiMusicGenerator.MusicLayer.LeadMelody)
				ui.WriteLine("E=Edit Seed (step sequencer)");

			if (item.Kind == ItemKind.Layer && ParseLayer(item.Id) == LoFiMusicGenerator.MusicLayer.Bass)
				ui.WriteLine("E=Edit Bass Pattern (step sequencer)");
		}
    }

    private void RenderMelodySeed(UiContext ui)
    {
        var snapshot = PageMusicSystem.GetTuningSnapshot();
        int len = Math.Clamp(snapshot.MelodySeedLength, 4, 64);
        var seed = snapshot.MelodySeed ?? Array.Empty<int?>();

        if (seed.Length < len)
            len = seed.Length;

        _seedIndex = Math.Clamp(_seedIndex, 0, Math.Max(0, len - 1));

        ui.WriteLine("Lead Melody Seed Editor");
        ui.WriteLine();
        ui.WriteLine("Primer:");
        ui.WriteLine("- The generator reads one seed step per beat.");
        ui.WriteLine("- Each step is semitones from the current chord root.");
        ui.WriteLine("- --- means rest (no note on that beat).");
        ui.WriteLine("- If you don't hear changes, make sure: Lead is enabled, Volume > 0, and Use Seed = ON.");
        ui.WriteLine();
        ui.WriteLine("Left/Right=Move  Up/Down=Pitch  PgUp/PgDn=Octave  Space=Toggle Rest");
        ui.WriteLine("B/Esc=Back");
        ui.WriteLine();

        const int cols = 12;
        for (int rowStart = 0; rowStart < len; rowStart += cols)
        {
            int rowEnd = Math.Min(len, rowStart + cols);
            var line = new System.Text.StringBuilder();

            for (int i = rowStart; i < rowEnd; i++)
            {
                string stepText = seed[i].HasValue
                    ? seed[i]!.Value.ToString("+00;-00;00")
                    : "---";

                if (i == _seedIndex)
                    line.Append('[').Append(stepText).Append("] ");
                else
                    line.Append(' ').Append(stepText).Append("  ");
            }

            ui.WriteLine(line.ToString());
        }

        ui.WriteLine();
        ui.WriteLine($"Seed Length: {snapshot.MelodySeedLength}   Use Seed: {(snapshot.MelodyUseSeed ? "ON" : "OFF")}");
    }

    private void RenderBassPattern(UiContext ui)
    {
        var snapshot = PageMusicSystem.GetTuningSnapshot();
        int len = Math.Clamp(snapshot.BassPatternLength, 4, 64);
        var pat = snapshot.BassPattern ?? Array.Empty<int?>();

        if (pat.Length < len)
            len = pat.Length;

        _bassSeedIndex = Math.Clamp(_bassSeedIndex, 0, Math.Max(0, len - 1));

        ui.WriteLine("Bass Pattern Editor");
        ui.WriteLine();
        ui.WriteLine("Primer:");
        ui.WriteLine("- The bass reads one step per beat.");
        ui.WriteLine("- Each step is semitones from the current chord root.");
        ui.WriteLine("- --- means rest (no bass note on that beat).");
        ui.WriteLine();
        ui.WriteLine("Left/Right=Move  Up/Down=Pitch  PgUp/PgDn=Octave  Space=Toggle Rest");
        ui.WriteLine("B/Esc=Back");
        ui.WriteLine();

        const int cols = 12;
        for (int rowStart = 0; rowStart < len; rowStart += cols)
        {
            int rowEnd = Math.Min(len, rowStart + cols);
            var line = new System.Text.StringBuilder();

            for (int i = rowStart; i < rowEnd; i++)
            {
                string stepText = pat[i].HasValue
                    ? pat[i]!.Value.ToString("+00;-00;00")
                    : "---";

                if (i == _bassSeedIndex)
                    line.Append('[').Append(stepText).Append("] ");
                else
                    line.Append(' ').Append(stepText).Append("  ");
            }

            ui.WriteLine(line.ToString());
        }

        ui.WriteLine();
        ui.WriteLine($"Pattern Length: {snapshot.BassPatternLength}");
    }

    private void RenderSavePreset(UiContext ui)
    {
        ui.WriteLine("Save Music Preset");
        ui.WriteLine();
        ui.WriteLine("Type a name and press Enter.");
        ui.WriteLine("Esc/B goes back.");
        ui.WriteLine();
        ui.WriteLine($"> {_presetNameInput}");
    }

    private void RenderLoadPreset(UiContext ui)
    {
        ui.WriteLine("Load Music Preset");
        ui.WriteLine();

        if (_presetNames.Length == 0)
        {
            ui.WriteLine("(No presets found)");
            ui.WriteLine();
            ui.WriteLine("Press B to go back.");
            return;
        }

        int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight - 6 : 12;
        viewport = Math.Max(6, viewport);

        int maxScroll = Math.Max(0, _presetNames.Length - viewport);
        _presetScroll = Math.Clamp(_presetScroll, 0, maxScroll);
        _presetSelected = Math.Clamp(_presetSelected, 0, _presetNames.Length - 1);

        if (_presetSelected < _presetScroll) _presetScroll = _presetSelected;
        if (_presetSelected >= _presetScroll + viewport) _presetScroll = _presetSelected - viewport + 1;

        int end = Math.Min(_presetNames.Length, _presetScroll + viewport);
        for (int i = _presetScroll; i < end; i++)
        {
            string cursor = (i == _presetSelected) ? ">" : " ";
            ui.WriteLine($"{cursor} {_presetNames[i]}");
        }

        ui.WriteLine();
		ui.WriteLine("↩=Load  (B)ack");
    }

    protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
    {
        if (_mode == Mode.MelodySeed)
        {
            var snapshot = PageMusicSystem.GetTuningSnapshot();
            int len = Math.Clamp(snapshot.MelodySeedLength, 4, 64);
            var seed = snapshot.MelodySeed ?? Array.Empty<int?>();
            if (seed.Length < len)
                len = seed.Length;

            _seedIndex = Math.Clamp(_seedIndex, 0, Math.Max(0, len - 1));

            int? cur = (len > 0) ? seed[_seedIndex] : null;

            switch (key.Key)
            {
                case ConsoleKey.B:
                case ConsoleKey.Backspace:
                case ConsoleKey.Escape:
                    _mode = Mode.Edit;
                    return PageResult.Stay;

                case ConsoleKey.LeftArrow:
                    _seedIndex = Math.Clamp(_seedIndex - 1, 0, Math.Max(0, len - 1));
                    return PageResult.Stay;

                case ConsoleKey.RightArrow:
                    _seedIndex = Math.Clamp(_seedIndex + 1, 0, Math.Max(0, len - 1));
                    return PageResult.Stay;

                case ConsoleKey.UpArrow:
                    PageMusicSystem.SetMelodySeedStep(_seedIndex, (cur ?? 0) + 1);
                    return PageResult.Stay;

                case ConsoleKey.DownArrow:
                    PageMusicSystem.SetMelodySeedStep(_seedIndex, (cur ?? 0) - 1);
                    return PageResult.Stay;

                case ConsoleKey.PageUp:
                    PageMusicSystem.SetMelodySeedStep(_seedIndex, (cur ?? 0) + 12);
                    return PageResult.Stay;

                case ConsoleKey.PageDown:
                    PageMusicSystem.SetMelodySeedStep(_seedIndex, (cur ?? 0) - 12);
                    return PageResult.Stay;

                case ConsoleKey.Spacebar:
                    PageMusicSystem.SetMelodySeedStep(_seedIndex, cur.HasValue ? null : 0);
                    return PageResult.Stay;

                default:
                    return PageResult.Stay;
            }
        }

        if (_mode == Mode.BassPattern)
        {
            var snapshot = PageMusicSystem.GetTuningSnapshot();
            int len = Math.Clamp(snapshot.BassPatternLength, 4, 64);
            var pat = snapshot.BassPattern ?? Array.Empty<int?>();
            if (pat.Length < len)
                len = pat.Length;

            _bassSeedIndex = Math.Clamp(_bassSeedIndex, 0, Math.Max(0, len - 1));
            int? cur = (len > 0) ? pat[_bassSeedIndex] : null;

            switch (key.Key)
            {
                case ConsoleKey.B:
                case ConsoleKey.Backspace:
                case ConsoleKey.Escape:
                    _mode = Mode.Edit;
                    return PageResult.Stay;

                case ConsoleKey.LeftArrow:
                    _bassSeedIndex = Math.Clamp(_bassSeedIndex - 1, 0, Math.Max(0, len - 1));
                    return PageResult.Stay;

                case ConsoleKey.RightArrow:
                    _bassSeedIndex = Math.Clamp(_bassSeedIndex + 1, 0, Math.Max(0, len - 1));
                    return PageResult.Stay;

                case ConsoleKey.UpArrow:
                    global::Spacegun_Simulator.PageMusicSystem.SetBassPatternStep(_bassSeedIndex, (cur ?? 0) + 1);
                    return PageResult.Stay;

                case ConsoleKey.DownArrow:
                    global::Spacegun_Simulator.PageMusicSystem.SetBassPatternStep(_bassSeedIndex, (cur ?? 0) - 1);
                    return PageResult.Stay;

                case ConsoleKey.PageUp:
                    global::Spacegun_Simulator.PageMusicSystem.SetBassPatternStep(_bassSeedIndex, (cur ?? 0) + 12);
                    return PageResult.Stay;

                case ConsoleKey.PageDown:
                    global::Spacegun_Simulator.PageMusicSystem.SetBassPatternStep(_bassSeedIndex, (cur ?? 0) - 12);
                    return PageResult.Stay;

                case ConsoleKey.Spacebar:
                    global::Spacegun_Simulator.PageMusicSystem.SetBassPatternStep(_bassSeedIndex, cur.HasValue ? null : 0);
                    return PageResult.Stay;

                default:
                    return PageResult.Stay;
            }
        }

        if (_mode == Mode.SavePreset)
        {
            switch (key.Key)
            {
                case ConsoleKey.Escape:
                case ConsoleKey.B:
                case ConsoleKey.Backspace when (key.Modifiers & ConsoleModifiers.Control) != 0:
                    _mode = Mode.List;
                    return PageResult.Stay;

                case ConsoleKey.Backspace:
                    if (_presetNameInput.Length > 0)
                        _presetNameInput = _presetNameInput[..^1];
                    return PageResult.Stay;

                case ConsoleKey.Enter:
                    if (string.IsNullOrWhiteSpace(_presetNameInput))
                    {
                        _statusMessage = "Preset name cannot be empty.";
                        _mode = Mode.List;
                        return PageResult.Stay;
                    }

                    PageMusicSystem.SaveMusicPreset(_presetNameInput);
                    _statusMessage = $"Saved preset: {_presetNameInput}";
                    _mode = Mode.List;
                    return PageResult.Stay;

                default:
                    if (!char.IsControl(key.KeyChar))
                        _presetNameInput += key.KeyChar;
                    return PageResult.Stay;
            }
        }

        if (_mode == Mode.LoadPreset)
        {
            switch (key.Key)
            {
                case ConsoleKey.B:
                case ConsoleKey.Backspace:
                    _mode = Mode.List;
                    return PageResult.Stay;

                case ConsoleKey.UpArrow:
                    _presetSelected = Math.Clamp(_presetSelected - 1, 0, Math.Max(0, _presetNames.Length - 1));
                    return PageResult.Stay;

                case ConsoleKey.DownArrow:
                    _presetSelected = Math.Clamp(_presetSelected + 1, 0, Math.Max(0, _presetNames.Length - 1));
                    return PageResult.Stay;

                case ConsoleKey.PageUp:
                    _presetSelected = Math.Clamp(_presetSelected - 6, 0, Math.Max(0, _presetNames.Length - 1));
                    return PageResult.Stay;

                case ConsoleKey.PageDown:
                    _presetSelected = Math.Clamp(_presetSelected + 6, 0, Math.Max(0, _presetNames.Length - 1));
                    return PageResult.Stay;

                case ConsoleKey.Enter:
                    if (_presetNames.Length == 0) return PageResult.Stay;
                    string name = _presetNames[Math.Clamp(_presetSelected, 0, _presetNames.Length - 1)];
                    if (PageMusicSystem.LoadMusicPreset(name))
                        _statusMessage = $"Loaded preset: {name}";
                    else
                        _statusMessage = $"Failed to load preset: {name}";

                    _mode = Mode.List;
                    return PageResult.Stay;

                default:
                    return PageResult.Stay;
            }
        }

        if (_mode == Mode.Edit)
        {
            switch (key.Key)
            {
                case ConsoleKey.B:
                case ConsoleKey.Backspace:
                    _mode = Mode.List;
                    return PageResult.Stay;

                case ConsoleKey.Enter:
                    _mode = Mode.List;
                    return PageResult.Stay;

                case ConsoleKey.E:
                    {
                        if (IsMasterRowSelected())
                            return PageResult.Stay;

                        var item = GetSelectedItem();
                        if (item.Kind == ItemKind.Layer && ParseLayer(item.Id) == LoFiMusicGenerator.MusicLayer.LeadMelody)
                        {
                            _seedIndex = 0;
                            _mode = Mode.MelodySeed;
                        }

                        if (item.Kind == ItemKind.Layer && ParseLayer(item.Id) == LoFiMusicGenerator.MusicLayer.Bass)
                        {
                            _bassSeedIndex = 0;
                            _mode = Mode.BassPattern;
                        }

                        return PageResult.Stay;
                    }

                case ConsoleKey.Spacebar:
                    ToggleSelected();
                    return PageResult.Stay;

                case ConsoleKey.S:
                    _presetNameInput = string.Empty;
                    _mode = Mode.SavePreset;
                    return PageResult.Stay;

                case ConsoleKey.L:
                    _presetNames = PageMusicSystem.ListMusicPresets();
                    _presetSelected = 0;
                    _presetScroll = 0;
                    _mode = Mode.LoadPreset;
                    return PageResult.Stay;

                case ConsoleKey.UpArrow:
                    _editParamIndex = Math.Clamp(_editParamIndex - 1, 0, int.MaxValue);
                    return PageResult.Stay;

                case ConsoleKey.DownArrow:
                    _editParamIndex = Math.Clamp(_editParamIndex + 1, 0, int.MaxValue);
                    return PageResult.Stay;

                case ConsoleKey.LeftArrow:
                    AdjustSelectedParam(sign: -1, coarse: false);
                    return PageResult.Stay;

                case ConsoleKey.RightArrow:
                    AdjustSelectedParam(sign: 1, coarse: false);
                    return PageResult.Stay;

                case ConsoleKey.PageUp:
                    AdjustSelectedParam(sign: 1, coarse: true);
                    return PageResult.Stay;

                case ConsoleKey.PageDown:
                    AdjustSelectedParam(sign: -1, coarse: true);
                    return PageResult.Stay;

                default:
                    return PageResult.Stay;
            }
        }

        // List mode
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
                // Always allow navigation off the master row.
                return HandleListNavigation(key);

            case ConsoleKey.PageUp:
            case ConsoleKey.PageDown:
                // Coarse adjust master volume while the master row is selected.
                // PageUp/PageDown are treated as coarse controls elsewhere too.
                if (IsMasterRowSelected())
                {
                    float delta = key.Key == ConsoleKey.PageUp ? 0.10f : -0.10f;
                    PageMusicSystem.AdjustGlobal("Master", delta);
                    return PageResult.Stay;
                }

                return HandleListNavigation(key);

            case ConsoleKey.LeftArrow:
                if (IsMasterRowSelected())
                {
                    PageMusicSystem.AdjustGlobal("Master", -0.02f);
                    return PageResult.Stay;
                }
                return PageResult.Stay;

            case ConsoleKey.RightArrow:
                if (IsMasterRowSelected())
                {
                    PageMusicSystem.AdjustGlobal("Master", 0.02f);
                    return PageResult.Stay;
                }
                return PageResult.Stay;

            case ConsoleKey.Spacebar:
                ToggleSelected();
                return PageResult.Stay;

            case ConsoleKey.Enter:
				if (IsMasterRowSelected())
					return PageResult.Stay;

                _mode = Mode.Edit;
                _editParamIndex = 0;
                _editParamScroll = 0;
                return PageResult.Stay;

            case ConsoleKey.S:
                _presetNameInput = string.Empty;
                _mode = Mode.SavePreset;
                return PageResult.Stay;

            case ConsoleKey.L:
                _presetNames = PageMusicSystem.ListMusicPresets();
                _presetSelected = 0;
                _presetScroll = 0;
                _mode = Mode.LoadPreset;
                return PageResult.Stay;

            case ConsoleKey.B:
            case ConsoleKey.Backspace:
                return PageResult.Back(PageId.MainMenu);

            default:
                return PageResult.Stay;
        }
    }

    private PageResult HandleListNavigation(ConsoleKeyInfo key)
    {
        int step = key.Key switch
        {
            ConsoleKey.UpArrow => -1,
            ConsoleKey.DownArrow => 1,
            ConsoleKey.PageUp => -6,
            ConsoleKey.PageDown => 6,
            _ => 0
        };

        _selected = Math.Clamp(_selected + step, 0, GetListRowCount() - 1);
        return PageResult.Stay;
    }

    protected override PageResult HandleEscape(UiContext ui, ConsoleKeyInfo key)
    {
        // In the boot UI, ESC would otherwise be interpreted as app-exit.
        // Here, treat it as a safe "back" action instead.
        if (_mode == Mode.MelodySeed)
        {
            _mode = Mode.Edit;
            return PageResult.Stay;
        }

        if (_mode != Mode.List)
        {
            _mode = Mode.List;
            return PageResult.Stay;
        }

        return PageResult.Back(PageId.MainMenu);
    }

    private bool TryGetEnabled(Item item)
    {
        var snapshot = PageMusicSystem.GetTuningSnapshot();

        if (item.Kind == ItemKind.Layer)
        {
            var layer = ParseLayer(item.Id);
            return layer switch
            {
                LoFiMusicGenerator.MusicLayer.Chords => snapshot.EnableChords,
                LoFiMusicGenerator.MusicLayer.Bass => snapshot.EnableBass,
                LoFiMusicGenerator.MusicLayer.Pad => snapshot.EnablePad,
                LoFiMusicGenerator.MusicLayer.Bell => snapshot.EnableBell,
                LoFiMusicGenerator.MusicLayer.LeadMelody => snapshot.EnableLeadMelody,
                LoFiMusicGenerator.MusicLayer.Drums => snapshot.EnableDrums,
                LoFiMusicGenerator.MusicLayer.VinylCrackle => snapshot.EnableVinylCrackle,
                LoFiMusicGenerator.MusicLayer.BitCrush => snapshot.EnableBitCrush,
                LoFiMusicGenerator.MusicLayer.HiHat => snapshot.EnableHiHat,
                LoFiMusicGenerator.MusicLayer.Ride => snapshot.EnableRide,
                _ => true
            };
        }

        // Drum lane
        var lane = ParseLane(item.Id);
        return snapshot.DrumEnabled.TryGetValue(lane.ToString(), out var enabled) ? enabled : true;
    }

    private void ToggleSelected()
    {
        if (IsMasterRowSelected())
            return;

        var item = GetSelectedItem();
        bool enabled = TryGetEnabled(item);

        if (item.Kind == ItemKind.Layer)
        {
            var layer = ParseLayer(item.Id);
            PageMusicSystem.SetLayerEnabled(layer, !enabled);
            return;
        }

        var lane = ParseLane(item.Id);
        PageMusicSystem.SetDrumLaneEnabled(lane, !enabled);
    }

    private ParamDef[] BuildParams(Item item, LoFiMusicGenerator.AudioTuningSettings snapshot)
    {
        var list = new List<ParamDef>(12);

        if (item.Kind == ItemKind.Layer)
        {
            var layer = ParseLayer(item.Id);
            switch (layer)
            {
                case LoFiMusicGenerator.MusicLayer.Chords:
                    list.Add(new ParamDef("Volume", 0.0f, 0.25f, s => s.ChordLevel, d => PageMusicSystem.AdjustGlobal("Chord", d), 0.0025f, 0.01f));
                    list.Add(new ParamDef("Melody Follow", 0.0f, 1.0f, s => s.ChordsMelodyFollow, d => PageMusicSystem.AdjustGlobal("ChordsMelodyFollow", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Melody Drift", 0.0f, 1.0f, s => s.ChordsMelodyDrift, d => PageMusicSystem.AdjustGlobal("ChordsMelodyDrift", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Melody Mutation", 0.0f, 1.0f, s => s.ChordsMelodyMutation, d => PageMusicSystem.AdjustGlobal("ChordsMelodyMutation", d), 0.01f, 0.05f));
                    break;
                case LoFiMusicGenerator.MusicLayer.Bass:
                    list.Add(new ParamDef("Volume", 0.0f, 1.0f, s => s.BassLevel, d => PageMusicSystem.AdjustGlobal("Bass", d), 0.005f, 0.02f));
                    // Mixer: 0..10, mapped to 0..1 internally.
                    list.Add(new ParamDef(
                        "Follow Mix (0=Pattern 10=Melody)",
                        0.0f,
                        10.0f,
                        s => s.BassMelodyFollow * 10.0f,
                        d => PageMusicSystem.AdjustGlobal("BassMelodyFollow", d / 10.0f),
                        1.0f,
                        2.0f,
                        IsInteger: true
                    ));
                    list.Add(new ParamDef("Melody Drift", 0.0f, 1.0f, s => s.BassMelodyDrift, d => PageMusicSystem.AdjustGlobal("BassMelodyDrift", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Melody Mutation", 0.0f, 1.0f, s => s.BassMelodyMutation, d => PageMusicSystem.AdjustGlobal("BassMelodyMutation", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Pattern Length", 4.0f, 64.0f, s => s.BassPatternLength, d => PageMusicSystem.AdjustGlobal("BassPatternLength", d), 1.0f, 4.0f, IsInteger: true));
                    break;
                case LoFiMusicGenerator.MusicLayer.Pad:
                    list.Add(new ParamDef("Volume", 0.0f, 1.0f, s => s.PadLevel, d => PageMusicSystem.AdjustGlobal("Pad", d), 0.005f, 0.02f));
                    list.Add(new ParamDef("Melody Follow", 0.0f, 1.0f, s => s.PadMelodyFollow, d => PageMusicSystem.AdjustGlobal("PadMelodyFollow", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Melody Drift", 0.0f, 1.0f, s => s.PadMelodyDrift, d => PageMusicSystem.AdjustGlobal("PadMelodyDrift", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Melody Mutation", 0.0f, 1.0f, s => s.PadMelodyMutation, d => PageMusicSystem.AdjustGlobal("PadMelodyMutation", d), 0.01f, 0.05f));
                    break;
                case LoFiMusicGenerator.MusicLayer.Bell:
                    list.Add(new ParamDef("Volume", 0.0f, 1.0f, s => s.BellLevel, d => PageMusicSystem.AdjustGlobal("Bell", d), 0.005f, 0.02f));
                    list.Add(new ParamDef("Melody Follow", 0.0f, 1.0f, s => s.BellMelodyFollow, d => PageMusicSystem.AdjustGlobal("BellMelodyFollow", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Melody Drift", 0.0f, 1.0f, s => s.BellMelodyDrift, d => PageMusicSystem.AdjustGlobal("BellMelodyDrift", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Melody Mutation", 0.0f, 1.0f, s => s.BellMelodyMutation, d => PageMusicSystem.AdjustGlobal("BellMelodyMutation", d), 0.01f, 0.05f));
                    break;
                case LoFiMusicGenerator.MusicLayer.LeadMelody:
                    list.Add(new ParamDef("Volume", 0.0f, 0.5f, s => s.LeadLevel, d => PageMusicSystem.AdjustGlobal("Lead", d), 0.005f, 0.02f));
                    list.Add(new ParamDef("Melody Density", 0.0f, 1.0f, s => s.MelodyDensity, d => PageMusicSystem.AdjustGlobal("MelodyDensity", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Melody Mutation", 0.0f, 1.0f, s => s.MelodyMutation, d => PageMusicSystem.AdjustGlobal("MelodyMutation", d), 0.01f, 0.05f));
                    list.Add(new ParamDef("Use Seed", 0.0f, 1.0f, s => s.MelodyUseSeed ? 1.0f : 0.0f, _ => PageMusicSystem.AdjustGlobal("MelodyUseSeed", 0), 1.0f, 1.0f, IsInteger: true));
                    list.Add(new ParamDef("Seed Length", 4.0f, 64.0f, s => s.MelodySeedLength, d => PageMusicSystem.AdjustGlobal("MelodySeedLength", d), 1.0f, 4.0f, IsInteger: true));
                    break;
                case LoFiMusicGenerator.MusicLayer.Drums:
                    list.Add(new ParamDef("Drum Master", 0.0f, 4.0f, s => s.DrumMaster, d => PageMusicSystem.AdjustGlobal("DrumMaster", d), 0.02f, 0.10f));
                    list.Add(new ParamDef("Drum BPM", 40.0f, 240.0f, s => s.DrumBpm, d => PageMusicSystem.AdjustGlobal("DrumBPM", d), 1.0f, 5.0f, IsInteger: true));

                    // Expose per-lane gains here so "drum loop" instruments aren't hidden behind separate lane items.
                    var lanes = new[]
                    {
                        LoFiMusicGenerator.DrumLane.BD,
                        LoFiMusicGenerator.DrumLane.SD,
                        LoFiMusicGenerator.DrumLane.CH,
                        LoFiMusicGenerator.DrumLane.OH,
                        LoFiMusicGenerator.DrumLane.CY,
                        LoFiMusicGenerator.DrumLane.CP,
                        LoFiMusicGenerator.DrumLane.CB,
                        LoFiMusicGenerator.DrumLane.RS,
                        LoFiMusicGenerator.DrumLane.HT,
                        LoFiMusicGenerator.DrumLane.MT,
                        LoFiMusicGenerator.DrumLane.LT,
                        LoFiMusicGenerator.DrumLane.AC,
                        LoFiMusicGenerator.DrumLane.GH,
                    };

                    foreach (var lane in lanes)
                    {
                        list.Add(new ParamDef(
                            $"{lane} Gain",
                            0.0f,
                            4.0f,
                            s => s.DrumGains.TryGetValue(lane.ToString(), out var g) ? g : 1.0f,
                            d => PageMusicSystem.AdjustDrumLaneGain(lane, d),
                            0.02f,
                            0.10f
                        ));
                    }
                    break;
                case LoFiMusicGenerator.MusicLayer.VinylCrackle:
                    list.Add(new ParamDef("Crackle Level", 0.0f, 4.0f, s => s.CrackleLevel, d => PageMusicSystem.AdjustGlobal("Crackle", d), 0.02f, 0.10f));
                    break;
                case LoFiMusicGenerator.MusicLayer.BitCrush:
                    list.Add(new ParamDef("Crush Bits", 4.0f, 16.0f, s => s.BitCrushBits, d => PageMusicSystem.AdjustGlobal("BitCrushBits", d), 1.0f, 1.0f, IsInteger: true));
                    list.Add(new ParamDef("Crush Mix", 0.0f, 1.0f, s => s.BitCrushMix, d => PageMusicSystem.AdjustGlobal("BitCrushMix", d), 0.01f, 0.05f));
                    break;
            }
        }
        else
        {
            var lane = ParseLane(item.Id);
            list.Add(new ParamDef("Lane Gain", 0.0f, 4.0f, s =>
            {
                return s.DrumGains.TryGetValue(lane.ToString(), out var g) ? g : 1.0f;
            }, d => PageMusicSystem.AdjustDrumLaneGain(lane, d), 0.02f, 0.10f));
        }

        // Global effects (available regardless of instrument selection)
        list.Add(new ParamDef("BPM", 40.0f, 200.0f, s => s.Bpm, d => PageMusicSystem.AdjustGlobal("BPM", d), 1.0f, 5.0f, IsInteger: true));
        list.Add(new ParamDef("LowPass", 0.0f, 1.0f, s => s.LowPass, d => PageMusicSystem.AdjustGlobal("LowPass", d), 0.01f, 0.05f));
        list.Add(new ParamDef("Delay Mix", 0.0f, 1.0f, s => s.DelayMix, d => PageMusicSystem.AdjustGlobal("DelayMix", d), 0.01f, 0.05f));
        list.Add(new ParamDef("Delay Feedback", 0.0f, 0.98f, s => s.DelayFeedback, d => PageMusicSystem.AdjustGlobal("DelayFeedback", d), 0.01f, 0.05f));
        list.Add(new ParamDef("Delay Time (ms)", 0.0f, 1800.0f, s => s.DelayTimeMs, d => PageMusicSystem.AdjustGlobal("DelayTimeMs", d), 10.0f, 50.0f));
        list.Add(new ParamDef("Reverb Mix", 0.0f, 1.0f, s => s.ReverbMix, d => PageMusicSystem.AdjustGlobal("ReverbMix", d), 0.01f, 0.05f));
        list.Add(new ParamDef("Chorus Mix", 0.0f, 1.0f, s => s.ChorusMix, d => PageMusicSystem.AdjustGlobal("ChorusMix", d), 0.01f, 0.05f));

        return list.ToArray();
    }

    private ParamDef[] BuildMasterParams(LoFiMusicGenerator.AudioTuningSettings snapshot)
    {
        return
        [
            new ParamDef("Master Volume", 0.0f, 4.0f, s => s.Master, d => PageMusicSystem.AdjustGlobal("Master", d), 0.02f, 0.10f)
        ];
    }

    private void AdjustSelectedParam(int sign, bool coarse)
    {
        var snapshot = PageMusicSystem.GetTuningSnapshot();
		var parameters = IsMasterRowSelected()
			? BuildMasterParams(snapshot)
			: BuildParams(GetSelectedItem(), snapshot);
        if (parameters.Length == 0) return;

        _editParamIndex = Math.Clamp(_editParamIndex, 0, parameters.Length - 1);
        var p = parameters[_editParamIndex];
        float step = coarse ? p.CoarseStep : p.FineStep;
        float delta = sign * step;
        p.Adjust(delta);
    }

    private static string RenderBar(float value, float min, float max, int width)
    {
        if (width < 4) width = 4;
        if (max <= min) max = min + 1;

        float t = (value - min) / (max - min);
        t = Math.Clamp(t, 0.0f, 1.0f);
        int filled = (int)MathF.Round(t * width);

        return "[" + new string('#', filled) + new string('-', Math.Max(0, width - filled)) + "]";
    }

    private static LoFiMusicGenerator.MusicLayer ParseLayer(string itemId)
    {
        var key = itemId.AsSpan();
        const string prefix = "layer:";
        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            key = key[prefix.Length..];

        return Enum.Parse<LoFiMusicGenerator.MusicLayer>(key, ignoreCase: true);
    }

    private static LoFiMusicGenerator.DrumLane ParseLane(string itemId)
    {
        var key = itemId.AsSpan();
        const string prefix = "lane:";
        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            key = key[prefix.Length..];

        return Enum.Parse<LoFiMusicGenerator.DrumLane>(key, ignoreCase: true);
    }
}
