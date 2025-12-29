using System;
using System.Collections.Generic;
using Spacegun_Simulator.UI.Pages;

namespace Spacegun_Simulator.UI
{
    /// <summary>
    /// Owns navigation and page lifecycle for a single UI run.
    /// </summary>
    public sealed class UiController
    {
        private readonly Dictionary<string, IPage> _pages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _navStack = new();
        private readonly UiContext _ui;

        private string _currentPageId;

        public UiController(UiContext ui, string startPageId)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _currentPageId = startPageId ?? throw new ArgumentNullException(nameof(startPageId));
        }

        public void Register(IPage page)
        {
            if (page is null) throw new ArgumentNullException(nameof(page));
            _pages[page.Id] = page;

            // Debug Page Migration
            _ui.DebugLog($"REGISTER '{page.Id}' -> {page.GetType().Name}");
        }

        public void Run()
        {
            if (!_pages.ContainsKey(_currentPageId))
                throw new InvalidOperationException($"Start page '{_currentPageId}' is not registered.");

            // Debug Page Migration
            _ui.DebugLog($"RUN startPage='{_currentPageId}'");

            Enter(_currentPageId);

            while (true)
            {
                // If a page requested a session-level return-to-menu (ESC intent),
                // exit this controller immediately. Caller decides autosave + menu flow.
                if (_ui.RequestReturnToMenu || _ui.RequestExitGame)
                {
                    // Debug Page Migration
                    _ui.DebugLog($"RUN exit: RequestReturnToMenu={_ui.RequestReturnToMenu} RequestExitGame={_ui.RequestExitGame}");
                    break;
                }

                var current = _pages[_currentPageId];

                // Debug Page Migration
                _ui.DebugLog($"RENDER page='{_currentPageId}' type={current.GetType().Name}");

                current.Render(_ui);

                // Re-check after render (a page could set intent during render in future)
                if (_ui.RequestReturnToMenu || _ui.RequestExitGame)
                {
                    // Debug Page Migration
                    _ui.DebugLog($"RUN exit after render: RequestReturnToMenu={_ui.RequestReturnToMenu} RequestExitGame={_ui.RequestExitGame}");
                    break;
                }

                var key = _ui.ReadKey(intercept: true);

                var result = current.HandleInput(_ui, key);

                // Debug Page Migration
                _ui.DebugLog($"INPUT page='{_currentPageId}' key={key.Key} -> exit={result.ExitRequested} back={result.BackRequested} stay={result.StayOnPage} next='{result.NextPageId}'");

                // If ESC/Q happened, PageBase will have set the session-level intent and returned Exit.
                if (_ui.RequestReturnToMenu || _ui.RequestExitGame)
                {
                    // Debug Page Migration
                    _ui.DebugLog($"RUN exit: RequestReturnToMenu={_ui.RequestReturnToMenu} RequestExitGame={_ui.RequestExitGame} (after input)");
                    break;
                }

                if (result.ExitRequested)
                    break;

                if (result.BackRequested)
                {
                    if (!TryNavigateBack())
                    {
                        if (!string.IsNullOrWhiteSpace(result.BackFallbackPageId))
                            NavigateTo(result.BackFallbackPageId!);
                        else
                            break;
                    }

                    continue;
                }

                if (result.StayOnPage || string.IsNullOrWhiteSpace(result.NextPageId))
                    continue;

                NavigateTo(result.NextPageId!);
            }
        }

        public void NavigateTo(string nextPageId)
        {
            if (string.IsNullOrWhiteSpace(nextPageId))
            {
                // Debug Page Migration
                _ui.DebugLog($"WARN NavigateTo called with empty id from '{_currentPageId}'.");
                return;
            }

            if (!_pages.ContainsKey(nextPageId))
            {
                // Debug Page Migration
                _ui.DebugLog($"ERROR NavigateTo failed: '{nextPageId}' not registered. Known=[{string.Join(", ", _pages.Keys)}]");
                throw new InvalidOperationException($"Page '{nextPageId}' is not registered.");
            }

            // Debug Page Migration
            _ui.DebugLog($"NAV NavigateTo '{nextPageId}' from '{_currentPageId}'");

            Exit(_currentPageId);

            _navStack.Push(_currentPageId);
            _currentPageId = nextPageId;

            Enter(_currentPageId);
        }

        public bool TryNavigateBack()
        {
            if (_navStack.Count == 0)
                return false;

            var prev = _navStack.Pop();

            // Debug Page Migration
            _ui.DebugLog($"NAV Back '{prev}' from '{_currentPageId}'");

            Exit(_currentPageId);

            _currentPageId = prev;
            Enter(_currentPageId);

            return true;
        }

        private void Enter(string pageId)
        {
            // Debug Page Migration
            _ui.DebugLog($"NAV Enter '{pageId}'");

            // Start/transition background music on page enter (legacy behavior via PageMusicSystem).
            // Safe to ignore failures so UI never crashes due to audio.
            try { PageMusicSystem.PlayForPage(pageId); }
            catch (Exception ex)
            {
                // Debug Page Migration
                _ui.DebugLog($"WARN Audio PlayForPage('{pageId}') failed: {ex.GetType().Name}: {ex.Message}");
            }

            _pages[pageId].OnEnter(_ui);
        }

        private void Exit(string pageId)
        {
            // Debug Page Migration
            _ui.DebugLog($"NAV Exit '{pageId}'");

            _pages[pageId].OnExit(_ui);
        }
    }
}
