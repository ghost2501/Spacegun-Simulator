using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Screen;
using System;
using System.Collections.Generic;
using System.IO;

namespace Spacegun_Simulator
{
    /// <summary>
    /// Small descriptor for a framed page header.
    /// </summary>
    internal sealed class PageDescriptor
    {
        public IList<string> HeaderLines { get; }
        public string? LeftOverride { get; }
        public string? RightOverride { get; }

        public PageDescriptor(IList<string> headerLines, string? leftOverride = null, string? rightOverride = null)
        {
            HeaderLines = headerLines ?? new List<string>();
            LeftOverride = leftOverride;
            RightOverride = rightOverride;
        }
    }

    /// <summary>
    /// Centralized page renderer that draws header (raw) and optionally buffers content.
    /// </summary>
    internal sealed class PageRenderer
    {
        private readonly ScreenLayout _layout;
        private readonly TextWriter? _originalOut;
        private readonly TextWriter _indentWriter;
        private readonly int _globalIndent;

        public PageRenderer(ScreenLayout layout, TextWriter? originalOut, TextWriter indentWriter, int globalIndent)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _originalOut = originalOut;
            _indentWriter = indentWriter ?? throw new ArgumentNullException(nameof(indentWriter));
            _globalIndent = Math.Max(0, globalIndent);
        }

        /// <summary>
        /// Renders header and runs the content action. If buffered==true content writes are captured and flushed
        /// at the correct raw column so headers and content line up.
        /// </summary>
        public void RenderPage(PageDescriptor desc, Action contentWriter, bool buffered = true, bool respectSideArtHeight = false)
        {
            if (desc == null) throw new ArgumentNullException(nameof(desc));
            if (contentWriter == null) throw new ArgumentNullException(nameof(contentWriter));

            if (buffered)
            {
                // BeginBufferedFrame now accepts an opt-in flag to account for side-art height.
                var (contentLeft, contentTop) = _layout.BeginBufferedFrame(
                    desc.HeaderLines,
                    _originalOut,
                    _indentWriter,
                    _globalIndent,
                    desc.LeftOverride,
                    desc.RightOverride,
                    respectSideArtHeight);

                try
                {
                    contentWriter();
                }
                finally
                {
                    _layout.EndBufferedFrame(contentLeft, contentTop);
                }

                return;
            }

            // Non-buffered path
            Console.SetOut(_originalOut ?? Console.Out);
            Console.Clear();
            _layout.RenderWithSides_NoOffset(desc.HeaderLines, desc.LeftOverride, desc.RightOverride);

            int contentLeftNoOffset = _layout.CalculateContentLeft_NoOffset(
                desc.HeaderLines,
                desc.LeftOverride,
                desc.RightOverride);

            Console.SetOut(_indentWriter);
            try
            {
                Console.SetCursorPosition(Math.Max(0, contentLeftNoOffset + _globalIndent), Console.CursorTop);
            }
            catch { }

            contentWriter();
        }
    }

    /// <summary>
    /// Centralized table for per-page side art overrides.
    /// Add or edit entries here to specify custom left/right art for any page.
    /// 
    /// Usage:
    ///   - Just provide the filename (e.g., "MenuLeft.txt") - the path is added automatically
    ///   - Use null for either side to fall back to the default art
    ///   - Both null means use default art on both sides
    /// </summary>
    public static class PageArtOverrides
    {
        private static readonly string ART_PATH = Path.Combine(AppContext.BaseDirectory, "Assets", "ascii-art");

        /// <summary>
        /// Master table of page art overrides.
        /// Format: [PageKey] = (LeftArtFileName, RightArtFileName)
        /// Just provide filenames - the path "./Assets/ascii-art/" is added automatically.
        /// </summary>
        public static readonly Dictionary<string, (string? Left, string? Right)> Overrides = new()
        {
            [PageId.Title] = (null, null),
            [PageId.MainMenu] = (null, null),
            [PageId.DifficultySelection] = (null, null),
            [PageId.Detection] = (null, null),
            ["GameOver"] = ("GameOver.txt", "GameOver.txt"),
            ["ResourceAllocation"] = (null, null),
            ["ResourceOptions"] = (null, null),
            ["PreparationSummary"] = (null, null),
            ["ResearchMenu"] = (null, null),
            ["PreparationStatus"] = (null, null),
            ["WeaponDevelopment"] = (null, null),
            ["ProjectileDevelopment"] = (null, null),
            ["ProjectileConfigSummary"] = (null, null),
            ["GunDevelopment"] = (null, null),
            ["Firing"] = (null, null),
            ["MotionComputer"] = (null, null),
            ["TrajectoryPlotter"] = (null, null),
            ["FireSimulator"] = (null, null),
            ["EnterFiringParameters"] = (null, null),
            ["DetailedWeaponStatus"] = (null, null),
        };

        /// <summary>
        /// Returns the full paths for the given pageKey, or (null, null) if not set.
        /// Automatically prepends "./Assets/ascii-art/" to any non-null filenames.
        /// </summary>
        public static (string? Left, string? Right) Get(string? pageKey)
        {
            if (string.IsNullOrEmpty(pageKey))
                return (null, null);

            if (!Overrides.TryGetValue(pageKey, out var pair))
                return (null, null);

            // Automatically add path prefix to any non-null filenames
            string? leftPath = pair.Left != null ? Path.Combine(ART_PATH, pair.Left) : null;
            string? rightPath = pair.Right != null ? Path.Combine(ART_PATH, pair.Right) : null;

            return (leftPath, rightPath);
        }

        /// <summary>
        /// Checks if a page has custom art defined (even if both are null overrides).
        /// </summary>
        public static bool HasEntry(string? pageKey)
        {
            return !string.IsNullOrEmpty(pageKey) && Overrides.ContainsKey(pageKey);
        }
    }
}