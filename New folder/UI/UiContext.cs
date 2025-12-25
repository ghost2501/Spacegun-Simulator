using System;

namespace Spacegun_Simulator.UI
{
    /// <summary>
    /// Shared UI services + references. Keep this as the "only" object pages rely on.
    /// Later you can add GameState, SaveSystem, Audio, etc.
    /// </summary>
    public sealed class UiContext
    {
        // Navigation helpers (UiController sets these)
        internal Func<string>? _getPreviousPageId;

        /// <summary>Whether the controller has a navigation stack to return to.</summary>
        public bool CanGoBack { get; internal set; }

        /// <summary>Where Escape should go by default.</summary>
        public string BackTargetPageId { get; internal set; } = "MainMenu";

        // Output abstraction (minimal for now)
        public void Clear() => Console.Clear();
        public void Write(string text) => Console.Write(text);
        public void WriteLine(string text = "") => Console.WriteLine(text);

        // Input abstraction (minimal for now)
        public ConsoleKeyInfo ReadKey(bool intercept = true) => Console.ReadKey(intercept);

        // Optional hooks you can wire later
        public Action<string>? TryAutoSave { get; set; }
        public Action<string>? Log { get; set; }
    }
}
