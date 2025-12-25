using System;
using System.Collections.Generic;
using Spacegun_Simulator.UI.Pages;

namespace Spacegun_Simulator.UI
{
    /// <summary>
    /// Owns navigation and global UI behaviors.
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
        }

        public void Run()
        {
            if (!_pages.ContainsKey(_currentPageId))
                throw new InvalidOperationException($"Start page '{_currentPageId}' is not registered.");

            Enter(_currentPageId);

            while (true)
            {
                var current = _pages[_currentPageId];

                current.Render(_ui);

                var key = _ui.ReadKey(intercept: true);
                var result = current.HandleInput(_ui, key);

                if (result.ExitRequested)
                    break;

                if (result.StayOnPage || string.IsNullOrWhiteSpace(result.NextPageId))
                    continue;

                NavigateTo(result.NextPageId!);
            }
        }

        public void NavigateTo(string nextPageId)
        {
            if (!_pages.ContainsKey(nextPageId))
                throw new InvalidOperationException($"Page '{nextPageId}' is not registered.");

            Exit(_currentPageId);

            // Push current to back-stack
            _navStack.Push(_currentPageId);
            _ui.CanGoBack = _navStack.Count > 0;
            _ui.BackTargetPageId = _navStack.Count > 0 ? _navStack.Peek() : "MainMenu";

            _currentPageId = nextPageId;
            Enter(_currentPageId);
        }

        public void NavigateBack()
        {
            if (_navStack.Count == 0)
                return;

            Exit(_currentPageId);

            _currentPageId = _navStack.Pop();
            _ui.CanGoBack = _navStack.Count > 0;
            _ui.BackTargetPageId = _navStack.Count > 0 ? _navStack.Peek() : "MainMenu";

            Enter(_currentPageId);
        }

        private void Enter(string pageId)
        {
            _ui.CanGoBack = _navStack.Count > 0;
            _ui.BackTargetPageId = _navStack.Count > 0 ? _navStack.Peek() : "MainMenu";
            _pages[pageId].OnEnter(_ui);
        }

        private void Exit(string pageId)
        {
            _pages[pageId].OnExit(_ui);
        }
    }
}
