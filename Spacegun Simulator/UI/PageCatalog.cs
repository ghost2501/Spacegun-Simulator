using Spacegun_Simulator.UI.Diagnostics.Pages;
using Spacegun_Simulator.UI.Pages.Core;
using Spacegun_Simulator.UI.Pages.Development;
using Spacegun_Simulator.UI.Pages.FireControl;
using Spacegun_Simulator.UI.Pages.Resources;
using Spacegun_Simulator.UI.Pages.Audio;

namespace Spacegun_Simulator.UI
{
    /// <summary>
    /// Centralized registration for page-based UI.
    ///
    /// Note: pages that require runtime constructor parameters (e.g. EnterFiringParametersPage(maxVelocity),
    /// FiringResultsPage(lines)) are intentionally not registered here.
    /// </summary>
    public static class PageCatalog
    {
        public static void RegisterDiagnosticsMenu(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            controller.Register(new TestModeMenuPage());
            controller.Register(new FiringChallengePage());
            controller.Register(new DiagnosticsTestHarnessPage());
            controller.Register(new DiagnosticsUiPageLauncherPage());
        }

        public static void RegisterCore(UiController controller, bool includeGameOver = true)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            controller.Register(new TitleScreenPage());
            controller.Register(new MainMenuPage());
            controller.Register(new MusicConfigurationPage());

            // Some callers want Escape/back to cancel instead of returning to the menu.
            controller.Register(new DifficultySelectionPage());

            if (includeGameOver)
                controller.Register(new GameOverPage());
        }

        public static void RegisterGamePhasePages(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            controller.Register(new DetectionPage());
            controller.Register(new ResourceAllocationPage());
            controller.Register(new ResourceOptionsPage());
            controller.Register(new ResearchMenuPage());
            controller.Register(new PreparationStatusPage());
            controller.Register(new PreparationSummaryPage());
            controller.Register(new DevelopmentPage());
            controller.Register(new DetailedWeaponStatusPage());
            controller.Register(new GunDevelopmentPage());
            controller.Register(new ProjectileDevelopmentPage());
            controller.Register(new FiringPhasePage());
            controller.Register(new WaveCompletePage());
        }

        public static void RegisterDetection(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            controller.Register(new DetectionPage());
        }

        public static void RegisterWaveComplete(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            controller.Register(new WaveCompletePage());
        }

        public static void RegisterGameOver(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            controller.Register(new GameOverPage());
        }

        public static void RegisterResourcePhasePages(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            controller.Register(new ResourceAllocationPage());
            controller.Register(new ResourceOptionsPage());
            controller.Register(new ResearchMenuPage());
            controller.Register(new PreparationStatusPage());
            controller.Register(new PreparationSummaryPage());
        }

        public static void RegisterDevelopmentPages(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            controller.Register(new DevelopmentPage());
            controller.Register(new DetailedWeaponStatusPage());
            controller.Register(new GunDevelopmentPage());
            controller.Register(new ProjectileDevelopmentPage());
        }

        public static void RegisterDevelopmentSubpages(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            controller.Register(new DetailedWeaponStatusPage());
            controller.Register(new GunDevelopmentPage());
            controller.Register(new ProjectileDevelopmentPage());
        }

        public static void RegisterFireControlTools(UiController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            controller.Register(new MotionComputerPage());
            controller.Register(new BallisticsTablesPage());
            controller.Register(new TrajectoryPlotterPage());
            controller.Register(new FireSimulatorPage());
        }
    }
}
