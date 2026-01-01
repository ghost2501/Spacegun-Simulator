namespace Spacegun_Simulator.Core
{
    /// <summary>
    /// Central switch for console text compatibility.
    /// When ASCII-only is enabled, UI output is sanitized to avoid Unicode glyphs
    /// that commonly break under non-UTF8 Windows console code pages.
    /// </summary>
    public static class ConsoleTextMode
    {
        public static bool AsciiOnly { get; private set; }

        public static bool AsciiOnlyForcedByUser { get; private set; }

        public static void EnableAsciiOnly(bool forcedByUser = false)
        {
            AsciiOnly = true;
            if (forcedByUser)
                AsciiOnlyForcedByUser = true;
        }

        public static string Sanitize(string? text)
        {
            if (!AsciiOnly)
                return text ?? "";

            if (string.IsNullOrEmpty(text))
                return "";

            // Fast path: already ASCII.
            bool needs = false;
            foreach (char ch in text)
            {
                if (ch > 0x7F) { needs = true; break; }
            }
            if (!needs)
                return text;

            // Replace a few common *semantic* glyphs with readable ASCII tokens.
            // Note: this may expand the string; callers that need fixed width should clamp after sanitizing.
            string expanded = text
                .Replace("←→", "</>", StringComparison.Ordinal)
                .Replace("↩", "Enter", StringComparison.Ordinal)
                .Replace("Δ", "Delta", StringComparison.Ordinal)
                .Replace("°", "deg", StringComparison.Ordinal)
                .Replace("←", "<", StringComparison.Ordinal)
                .Replace("→", ">", StringComparison.Ordinal);

            var sb = new System.Text.StringBuilder(expanded.Length);

            foreach (char ch in expanded)
            {
                // Keep replacements 1:1 to preserve UI layout.
                sb.Append(ch switch
                {
                    // Box drawing (light)
                    '│' => '|',
                    '┃' => '|',
                    '║' => '|',
                    '─' => '-',
                    '━' => '-',
                    '═' => '-',
                    '┌' => '+',
                    '┐' => '+',
                    '└' => '+',
                    '┘' => '+',
                    '├' => '+',
                    '┤' => '+',
                    '┬' => '+',
                    '┴' => '+',
                    '┼' => '+',
                    '┏' => '+',
                    '┓' => '+',
                    '┗' => '+',
                    '┛' => '+',
                    '╔' => '+',
                    '╗' => '+',
                    '╚' => '+',
                    '╝' => '+',
                    '╞' => '+',
                    '╡' => '+',
                    '╪' => '+',
                    '╤' => '+',
                    '╧' => '+',
                    '╫' => '+',

                    // Block / shade
                    '▓' => '#',
                    '▒' => '#',
                    '░' => '.',
                    '■' => '#',

                    // Common UI glyphs (still here as a backstop if they sneak in)
                    '↩' => 'E',
                    '✓' => 'v',
                    '✔' => 'v',
                    '✗' => 'x',
                    '✘' => 'x',
                    '∙' => '.',
                    '•' => '*',

                    // Greek-ish used in art
                    'Θ' => 'O',

                    // Default: replace any other non-ASCII with '?'
                    _ when ch <= 0x7F => ch,
                    _ => '?'
                });
            }

            return sb.ToString();
        }
    }
}
