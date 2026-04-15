using Flink.Config;

namespace Flink.Core;

/// <summary>
/// Assigns keyboard bindings to windows.
///
/// Rules:
/// 1. Configured apps get their fixed letter (e.g. "windowsterminal" → "t")
/// 2. Unconfigured apps get the first letter of their process name that is still free
/// 3. Single window per app: one letter ("t")
///    Multiple windows per app: two letters, second from QWERTY rows ("tq", "tw", "te"...)
/// </summary>
internal static class KeyBinder
{
    // QWERTY rows in order — top row first, then home row, then bottom row
    private static readonly char[] SecondChars = [
        'q','w','e','r','t','y','u','i','o','p',  // top row
        'a','s','d','f','g','h','j','k','l',       // home row
        'z','x','c','v','b','n','m'                // bottom row
    ];

    private static readonly char[] AlphabetOrder =
        "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    public static void AssignBindings(List<WindowInfo> windows, AppConfig config)
    {
        // Group by process name
        var groups = windows
            .GroupBy(w => w.ProcessName)
            .ToList();

        // Track which first-letters are taken
        var usedFirstLetters = new HashSet<char>();

        // Pass 1: assign configured bindings first
        var assignedGroups = new Dictionary<string, char>();

        foreach (var group in groups)
        {
            if (config.Bindings.TryGetValue(group.Key, out string? letter)
                && letter.Length == 1)
            {
                char c = char.ToLower(letter[0]);
                if (!usedFirstLetters.Contains(c))
                {
                    assignedGroups[group.Key] = c;
                    usedFirstLetters.Add(c);
                }
            }
        }

        // Pass 2: assign auto letters to unconfigured groups
        foreach (var group in groups)
        {
            if (assignedGroups.ContainsKey(group.Key))
                continue;

            char firstLetter = AssignFirstLetter(group.Key, usedFirstLetters);
            assignedGroups[group.Key] = firstLetter;
            usedFirstLetters.Add(firstLetter);
        }

        // Pass 3: assign actual bindings to each window
        foreach (var group in groups)
        {
            char letter = assignedGroups[group.Key];
            var groupWindows = group.ToList();

            if (groupWindows.Count == 1)
            {
                groupWindows[0].Binding = letter.ToString();
            }
            else
            {
                for (int i = 0; i < groupWindows.Count; i++)
                {
                    char second = i < SecondChars.Length
                        ? SecondChars[i]
                        : (char)('0' + (i - SecondChars.Length)); // fallback to digits
                    groupWindows[i].Binding = $"{letter}{second}";
                }
            }
        }
    }

    private static char AssignFirstLetter(string processName, HashSet<char> used)
    {
        // Try letters from process name first (ASCII only — no Unicode surprises)
        foreach (char c in processName.ToLowerInvariant())
        {
            if (char.IsAsciiLetter(c) && !used.Contains(c))
                return c;
        }

        // Fallback: first free letter of the alphabet
        foreach (char c in AlphabetOrder)
        {
            if (!used.Contains(c))
                return c;
        }

        return '?';
    }
}
