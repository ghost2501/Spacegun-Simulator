using System;
using System.Collections.Generic;
using System.IO;
using Spacegun_Simulator.UI.Screen;

namespace Spacegun_Simulator.UI
{
    /// <summary>
    /// Shared UI services and state.
    /// Pages should rely only on this object for external interaction.
    /// </summary>
    public sealed class UiContext
    {
        // ============================================================
        // Debug / diagnostics
        // ============================================================

        public bool DebugEnabled { get; set; } = true;

        public Action<string>? Log { get; set; }

        public void DebugLog(string message)
        {
            if (!DebugEnabled) return;
            try { Log?.Invoke(message); } catch { }
        }

        // ============================================================
        // Game session state
        // ============================================================

        /// <summary>
        /// The authoritative game state / engine for the current session.
        /// </summary>
        public GameState? Game { get; set; }

        // ============================================================
        // Session-level intent flags (set by pages, handled by caller)
        // ============================================================

        /// <summary>
        /// Set by pages when ESC is pressed.
        /// Caller (ConsoleUI / Program) decides how to autosave and return to menu.
        /// </summary>
        public bool RequestReturnToMenu { get; set; }

        // ============================================================
        // Layout / rendering
        // ============================================================

        public ScreenLayout Layout { get; }

        public TextWriter OriginalOut { get; }
        public TextWriter IndentWriter { get; }

        /// <summary>
        /// Legacy indent support (NOT used by page-based UI rendering).
        /// </summary>
        public int GlobalIndent { get; }

        /// <summary>
        /// Computed per frame by PageBase.
        /// Scrollable pages should respect this value.
        /// </summary>
        public int ContentViewportHeight { get; internal set; }

        // ============================================================
        // Output helpers
        // ============================================================

        public void Clear() => Console.Clear();
        public void Write(string text) => Console.Write(text);
        public void WriteLine(string text = "") => Console.WriteLine(text);

        // ============================================================
        // Header / footer / side art hooks
        // ============================================================

        /// <summary>
        /// Optional single-line status text (below title).
        /// </summary>
        public Func<string>? BuildStatusBar { get; set; }

        /// <summary>
        /// Optional single-line footer bar (above footer hint).
        /// </summary>
        public Func<string>? BuildFooterBar { get; set; }

        /// <summary>
        /// Header ASCII art (pageId -> lines).
        /// </summary>
        public Func<string, IReadOnlyList<string>?>? BuildHeaderArt { get; set; }

        /// <summary>
        /// Footer ASCII art (pageId -> lines).
        /// </summary>
        public Func<string, IReadOnlyList<string>?>? BuildFooterArt { get; set; }

        /// <summary>
        /// Resolves per-page side-art overrides.
        /// </summary>
        public Func<string, (string? Left, string? Right)> ResolveSideArt { get; set; }

        // ============================================================
        // Input
        // ============================================================

        public ConsoleKeyInfo ReadKey(bool intercept = true)
            => Console.ReadKey(intercept);

        // ============================================================
        // Optional hooks
        // ============================================================

        public Action<string>? TryAutoSave { get; set; }

        // ============================================================
        // Construction
        // ============================================================

        public UiContext(
            ScreenLayout layout,
            TextWriter? originalOut = null,
            TextWriter? indentWriter = null,
            int globalIndent = 0)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            OriginalOut = originalOut ?? Console.Out;
            IndentWriter = indentWriter ?? Console.Out;
            GlobalIndent = Math.Max(0, globalIndent);

            // Default: legacy side-art override table
            ResolveSideArt = pageId => PageArtOverrides.Get(pageId);

            // Default header bridge art (60 columns)
            BuildHeaderArt = _ => new[]
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░"
            };

            // Default footer bridge art (60 columns)
            BuildFooterArt = _ => new[]
            {
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓"
            };
        }
    }
}
