using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Pages.Core
{
    public sealed class TitleScreenPage : PageBase
    {
        public override string Id => PageId.Title;
        public override string Title => "SPACEGUN SIMULATOR";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: false,
            ShowSidePanels: false,
            FooterHint: null
        );

        public override void Render(UiContext ui)
        {
            // Render title art as a full-screen page (no PageBase frame/header).
            Console.Clear();

            string[] lines =
                (ConsoleTextMode.AsciiOnly ? LoadTitleArtLines("TitleScreen-ascii-only.txt") : null)
                ?? LoadTitleArtLines("TitleScreen.txt")
                ?? FallbackTitleArt();

            // Title screen writes directly to Console, so it must participate in ASCII fallback.
            for (int i = 0; i < lines.Length; i++)
                lines[i] = ConsoleTextMode.Sanitize(lines[i]);

            int winW, winH;
            try { winW = Console.WindowWidth; winH = Console.WindowHeight; }
            catch { winW = 120; winH = 40; }

            int artHeight = lines.Length;
            int artWidth = 0;
            for (int i = 0; i < lines.Length; i++)
                artWidth = Math.Max(artWidth, lines[i]?.Length ?? 0);

            int left = Math.Max(0, (winW - artWidth) / 2);
            int top = Math.Max(0, (winH - artHeight) / 2 - 1);

            // draw art
            for (int i = 0; i < artHeight; i++)
            {
                TrySetCursor(left, top + i);
                string line = lines[i] ?? string.Empty;

                // clip to window
                int maxLen = Math.Max(0, winW - left);
                if (line.Length > maxLen) line = line.Substring(0, maxLen);

                Console.Write(line);
            }

            // prompt
            string prompt = "Press any key to continue";
            int promptLeft = Math.Max(0, (winW - prompt.Length) / 2);
            int promptTop = Math.Min(winH - 2, top + artHeight + 1);

            TrySetCursor(promptLeft, promptTop);
            try
            {
                var prev = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(prompt);
                Console.ForegroundColor = prev;
            }
            catch
            {
                Console.Write(prompt);
            }

            // If UTF-8 setup failed and we fell back to ASCII, tell the player.
            if (ConsoleTextMode.AsciiOnly)
            {
                string notice = ConsoleTextMode.AsciiOnlyForcedByUser
                    ? "NOTICE: ASCII mode is enabled (forced). Remove --ascii-ui for full UI."
                    : "NOTICE: UTF-8 isn't available in this terminal; using ASCII mode. Use a UTF-8 capable terminal for full UI.";
                int noticeLeft = Math.Max(0, (winW - notice.Length) / 2);
                int noticeTop = Math.Min(winH - 1, promptTop + 1);

                TrySetCursor(noticeLeft, noticeTop);

                int maxLen = Math.Max(0, winW - noticeLeft);
                if (notice.Length > maxLen) notice = notice.Substring(0, maxLen);

                try
                {
                    var prev = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write(notice);
                    Console.ForegroundColor = prev;
                }
                catch
                {
                    Console.Write(notice);
                }
            }
        }

        protected override void RenderBody(UiContext ui) { /* unused; Render overridden */ }

        public override PageResult HandleInput(UiContext ui, ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Q)
            {
                ui.RequestExitGame = true;
                return PageResult.Exit;
            }

            // Title screen: ESC behaves like "any key" (continue).
            return PageResult.Go(PageId.MainMenu);
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
            => PageResult.Go(PageId.MainMenu);

        private static void TrySetCursor(int x, int y)
        {
            try { Console.SetCursorPosition(x, y); }
            catch { /* tiny console / unsupported */ }
        }

        private static string[]? LoadTitleArtLines(string fileName)
        {
            string cwd = Directory.GetCurrentDirectory();
            string baseDir = AppContext.BaseDirectory;

            string[] candidates =
            {
                Path.Combine(cwd, "Assets", "ascii-art", fileName),
                Path.Combine(baseDir, "Assets", "ascii-art", fileName),
                Path.GetFullPath(Path.Combine(baseDir, "..", "Assets", "ascii-art", fileName)),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Assets", "ascii-art", fileName)),
            };

            foreach (var p in candidates)
            {
                try
                {
                    if (File.Exists(p))
                        return File.ReadAllLines(p);
                }
                catch { }
            }

            return null;
        }

        private static string[] FallbackTitleArt() => new[]
        {
            "  _____                     _____             ",
            " / ____|                   / ____|            ",
            "| (___  _ __   __ _  ___  | (___   ___  _ __  ",
            " \\___ \\| '_ \\ / _` |/ _ \\  \\___ \\ / _ \\| '_ \\ ",
            " ____) | |_) | (_| |  __/  ____) | (_) | | | |",
            "|_____/| .__/ \\__,_|\\___| |_____/ \\___/|_| |_|",
            "       | |                                     ",
            "       |_|                                     ",
            "",
            "Terminal engineering sim. One shot per wave."
        };
    }
}