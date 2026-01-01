using Spacegun_Simulator.UI.Screen;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.Core;

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
        /// Caller (session flow / Program) decides how to autosave and return to menu.
        /// </summary>
        public bool RequestReturnToMenu { get; set; }

        /// <summary>
        /// Set by pages when Q is pressed.
        /// Caller (session flow / Program) decides how to terminate the app.
        /// </summary>
        public bool RequestExitGame { get; set; }

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

        /// <summary>
        /// Computed per frame by PageBase. Raw (no-indent) column where the center content starts.
        /// Useful for aligning legacy prompts (Console.ReadLine) to the left edge of the center frame.
        /// </summary>
        public int FrameContentLeftNoOffset { get; internal set; }

        /// <summary>
        /// Computed per frame by PageBase. Raw (no-indent) row where buffered content begins.
        /// </summary>
        public int FrameContentTopNoOffset { get; internal set; }

        // ============================================================
        // Output helpers
        // ============================================================

        public void Clear() => Console.Clear();
        public void Write(string text) => Console.Write(ConsoleTextMode.Sanitize(text));
        public void WriteLine(string text = "") => Console.WriteLine(ConsoleTextMode.Sanitize(text));

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

        /// <summary>
        /// Optional one-shot message to show on the next page render (typically the hub page).
        /// Pages may set this before navigating back.
        /// </summary>
        public string? FlashMessage { get; set; }

        // ============================================================
        // Navigation trace (set by UiController)
        // ============================================================

        /// <summary>
        /// The last page id that was active when a UiController exited.
        /// Used by higher-level flows/routers to decide what to do next.
        /// </summary>
        public string? LastPageId { get; internal set; }

        /// <summary>
        /// The last PageResult returned by the last page before a UiController exited.
        /// Null if the controller hasn't processed any input yet.
        /// </summary>
        public PageResult? LastPageResult { get; internal set; }

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
