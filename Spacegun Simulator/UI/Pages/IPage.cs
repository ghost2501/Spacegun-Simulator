using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages
{
    public interface IPage
    {
        /// <summary>Stable page identifier (use PageId constants).</summary>
        string Id { get; }

        /// <summary>Header shown at top of the page frame.</summary>
        string Title { get; }

        /// <summary>Per-page UI behavior overrides.</summary>
        PageChrome Chrome { get; }

        /// <summary>Called when page becomes active.</summary>
        void OnEnter(UiContext ui);

        /// <summary>Render the page.</summary>
        void Render(UiContext ui);

        /// <summary>Handle one key input event and return navigation result.</summary>
        PageResult HandleInput(UiContext ui, ConsoleKeyInfo key);

        /// <summary>Called when page is being left.</summary>
        void OnExit(UiContext ui);
    }
}
