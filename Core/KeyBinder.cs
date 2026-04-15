using Flink.Config;

namespace Flink.Core;

/// <summary>
/// Assigns keyboard bindings, display names, and clean titles to windows.
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
        var groups = windows.GroupBy(w => w.ProcessName).ToList();
        var usedFirstLetters = new HashSet<char>();
        var assignedGroups = new Dictionary<string, char>();

        // Pass 1: configured bindings first
        foreach (var group in groups)
        {
            if (config.Bindings.TryGetValue(group.Key, out string? letter) && letter.Length == 1)
            {
                char c = char.ToLower(letter[0]);
                if (!usedFirstLetters.Contains(c))
                {
                    assignedGroups[group.Key] = c;
                    usedFirstLetters.Add(c);
                }
            }
        }

        // Pass 2: auto-assign remaining
        foreach (var group in groups)
        {
            if (assignedGroups.ContainsKey(group.Key)) continue;

            char firstLetter = AssignFirstLetter(group.Key, usedFirstLetters);
            assignedGroups[group.Key] = firstLetter;
            usedFirstLetters.Add(firstLetter);
        }

        // Pass 3: set binding, display name, and clean title on each window
        foreach (var group in groups)
        {
            char letter = assignedGroups[group.Key];
            string displayName = ResolveDisplayName(group.Key, config);
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
                        : (char)('0' + (i - SecondChars.Length));
                    groupWindows[i].Binding = $"{letter}{second}";
                }
            }

            foreach (var w in groupWindows)
            {
                w.DisplayName = displayName;
                w.CleanTitle = StripAppSuffix(w.Title, displayName);
            }
        }
    }

    /// <summary>
    /// Returns the configured display name, or auto-capitalizes the process name.
    /// "windowsterminal" → "Windowsterminal" (if not in config)
    /// "windowsterminal" → "Terminal" (if configured)
    /// </summary>
    private static string ResolveDisplayName(string processName, AppConfig config)
    {
        if (config.Names.TryGetValue(processName, out string? name) && !string.IsNullOrEmpty(name))
            return name;

        // Auto-capitalize: "windowsterminal" → "Windowsterminal"
        if (string.IsNullOrEmpty(processName)) return processName;
        return char.ToUpper(processName[0]) + processName[1..];
    }

    /// <summary>
    /// Removes common " - AppName" suffixes that browsers and apps append to window titles.
    /// "GitHub - Zen Browser" → "GitHub"
    /// "Settings" → "Settings" (no suffix found, returned as-is)
    /// </summary>
    private static string StripAppSuffix(string title, string displayName)
    {
        if (string.IsNullOrEmpty(title)) return title;

        // Try common separators: " — ", " - ", " | "
        foreach (string sep in new[] { " \u2014 ", " - ", " | " })
        {
            int idx = title.LastIndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                string suffix = title[(idx + sep.Length)..].Trim();
                // Only strip if the suffix resembles the app name
                if (IsSimilar(suffix, displayName))
                    return title[..idx].Trim();
            }
        }

        return title;
    }

    /// <summary>
    /// Loose similarity check: does the suffix look like the app name?
    /// Handles "Zen Browser" matching "Zen", "Mozilla Firefox" matching "Firefox", etc.
    /// </summary>
    private static bool IsSimilar(string suffix, string displayName)
    {
        if (string.IsNullOrEmpty(suffix) || string.IsNullOrEmpty(displayName))
            return false;

        suffix = suffix.ToLowerInvariant();
        displayName = displayName.ToLowerInvariant();

        // Exact match
        if (suffix == displayName) return true;

        // Suffix contains a word from the display name (e.g. "Zen" in "Zen Browser")
        foreach (string word in displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length >= 3 && suffix.Contains(word))
                return true;
        }

        return false;
    }

    private static char AssignFirstLetter(string processName, HashSet<char> used)
    {
        foreach (char c in processName.ToLowerInvariant())
        {
            if (char.IsAsciiLetter(c) && !used.Contains(c))
                return c;
        }

        foreach (char c in AlphabetOrder)
        {
            if (!used.Contains(c))
                return c;
        }

        return '?';
    }
}
