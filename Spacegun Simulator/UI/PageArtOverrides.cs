using System;
using System.Collections.Generic;
using System.IO;

namespace Spacegun_Simulator
{
    // Small descriptor for a framed page header.
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

    // Centralized page renderer that draws header (raw) and optionally buffers content.
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

        // Renders header and runs the content action. If buffered==true content writes are captured and flushed
        // at the correct raw column so headers and content line up.
        public void RenderPage(PageDescriptor desc, Action contentWriter, bool buffered = true, bool respectSideArtHeight = false)
        {
            if (desc == null) throw new ArgumentNullException(nameof(desc));
            if (contentWriter == null) throw new ArgumentNullException(nameof(contentWriter));

            if (buffered)
            {
                // BeginBufferedFrame now accepts an opt-in flag to account for side-art height.
                var (contentLeft, contentTop) = _layout.BeginBufferedFrame(desc.HeaderLines, _originalOut, _indentWriter, _globalIndent, desc.LeftOverride, desc.RightOverride, respectSideArtHeight);

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

            // Non-buffered path remains unchanged
            Console.SetOut(_originalOut ?? Console.Out);
            Console.Clear();
            _layout.RenderWithSides_NoOffset(desc.HeaderLines, desc.LeftOverride, desc.RightOverride);

            int contentLeftNoOffset = _layout.CalculateContentLeft_NoOffset(desc.HeaderLines, desc.LeftOverride, desc.RightOverride);

            Console.SetOut(_indentWriter);
            try { Console.SetCursorPosition(Math.Max(0, contentLeftNoOffset + _globalIndent), Console.CursorTop); } catch { }

            contentWriter();
        }
    }
}