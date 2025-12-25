using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages
{
    /// <summary>
    /// Base class that centralizes default behavior so you don't duplicate it in every page.
    /// </summary>
    public abstract class PageBase : IPage
    {
        public abstract string Id { get; }
        public abstract string Title { get; }

        public virtual PageChrome Chrome { get; } = new();

        public virtual void OnEnter(UiContext ui)
        {
            // Hook autosave here later if desired:
            // if (Chrome.AutoSaveOnEnter) ui.TryAutoSave?.Invoke(Id);
        }

        public virtual void OnExit(UiContext ui)
        {
            // Hook autosave here later if desired:
            // if (Chrome.AutoSaveOnExit) ui.TryAutoSave?.Invoke(Id);
        }

        public virtual void Render(UiContext ui)
        {
            // Minimal default rendering. Replace with your frame renderer later.
            ui.Clear();
            ui.WriteLine($"=== {Title} ===");
            ui.WriteLine();
            RenderBody(ui);
            ui.WriteLine();
            ui.WriteLine(Chrome.FooterHint ?? "Press [Esc] to return, [Q] to quit.");
        }

        protected abstract void RenderBody(UiContext ui);

        public virtual PageResult HandleInput(UiContext ui, ConsoleKeyInfo key)
        {
            // Global defaults (can be overridden per page)
            if (key.Key == ConsoleKey.Q)
                return PageResult.Exit;

            if (key.Key == ConsoleKey.Escape)
                return ui.CanGoBack ? PageResult.Go(ui.BackTargetPageId) : PageResult.Stay;

            return HandleInputBody(ui, key);
        }

        protected virtual PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
            => PageResult.Stay;
    }
}
