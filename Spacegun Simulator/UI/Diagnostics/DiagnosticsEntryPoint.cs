using System;
using Spacegun_Simulator.UI.Diagnostics.Pages;
using Spacegun_Simulator.UI.Screen;

namespace Spacegun_Simulator.UI.Diagnostics
{
    public static class DiagnosticsEntryPoint
    {
        public static void Run(GameState game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            var originalOut = Console.Out;
            var indentWriter = new IndentTextWriter(originalOut, indentSpaces: 30);
            Console.SetOut(indentWriter);

            var layout = new ScreenLayout(offset: indentWriter.IndentLength, frameWidth: 60);

            var ui = new UiContext(
                layout: layout,
                originalOut: originalOut,
                indentWriter: indentWriter,
                globalIndent: indentWriter.IndentLength)
            {
                Game = game,
                DebugEnabled = false
            };

            var controller = new UiController(ui, PageId.TestModeMenu);
            controller.Register(new TestModeMenuPage());
            controller.Register(new FiringChallengePage());
            controller.Register(new DiagnosticsTestHarnessPage());
            controller.Register(new DiagnosticsUiPageLauncherPage());
            controller.Run();
        }
    }
}
