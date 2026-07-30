using System;

namespace Adnd.Game;

public static class InputHelper
{
    // Read a selection number using a single keypress when possible.
    // Falls back to reading the rest of the line if needed (for multi-digit input).
    public static int? ReadNumber(int min, int max)
    {
        var first = Console.ReadKey(true);

        // If user pressed Enter immediately, treat as no input
        if (first.Key == ConsoleKey.Enter)
            return null;
        // If the key produced a printable character, collect it and any immediately
        // available additional characters without blocking. This lets a single
        // keypress be accepted immediately while still allowing quick multi-digit
        // input if the user types several digits in rapid succession.
        string combined = string.Empty;

        if (first.KeyChar != '\0')
        {
            combined += first.KeyChar;

            // Pull in any buffered keystrokes immediately available
            while (Console.KeyAvailable)
            {
                var k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.Enter)
                    break;
                if (k.KeyChar != '\0')
                    combined += k.KeyChar;
            }
        }
        else
        {
            // Non-printable key pressed; attempt to read the rest of the line
            var line = Console.ReadLine();
            combined = line ?? string.Empty;
        }

        if (int.TryParse(combined.Trim(), out int val))
        {
            if (val >= min && val <= max)
                return val;
        }

        return null;
    }

    // Read a single letter key (A..Z) and return its zero-based index (A=0).
    // Returns null for Enter or invalid input.
    public static int? ReadLetterIndex(int count)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter)
            return null;

        if (key.KeyChar == '\0')
            return null;

        var ch = char.ToUpperInvariant(key.KeyChar);
        if (ch < 'A' || ch > 'Z')
            return null;

        int idx = ch - 'A';
        if (idx < 0 || idx >= count)
            return null;

        return idx;
    }
}
