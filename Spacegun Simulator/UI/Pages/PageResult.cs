namespace Spacegun_Simulator.UI.Pages
{
    /// <summary>
    /// Result of handling input on a page.
    /// </summary>
    public readonly record struct PageResult(
        string? NextPageId = null,
        bool StayOnPage = false,
        bool ExitRequested = false,
        bool BackRequested = false,
        string? BackFallbackPageId = null
    )
    {
        public static PageResult Stay => new(StayOnPage: true);
        public static PageResult Exit => new(ExitRequested: true);

        public static PageResult Go(string nextPageId) => new(NextPageId: nextPageId);

        public static PageResult Back(string? fallbackPageId = null)
            => new(BackRequested: true, BackFallbackPageId: fallbackPageId);
    }
}
