using Flink.Config;

namespace Flink.Core;

/// <summary>
/// Assigns and caches keyboard bindings for the lifetime of the session.
///
/// Rules:
/// 1. Configured apps get their fixed first letter (e.g. "windowsterminal" → "t")
/// 2. Unconfigured apps get the first free letter of their process name
/// 3. Once a first letter is assigned to a process, it never changes this session
/// 4. Once a HWND gets a second letter, it never changes this session
/// 5. Single window → one letter ("t"). Once a second window appears the process
///    switches to two-letter mode permanently for this session ("tq", "tw" ...)
/// </summary>
internal static class KeyBinder
{
    private static readonly char[] SecondChars = [
        'q','w','e','r','t','y','u','i','o','p',  // top row
        'a','s','d','f','g','h','j','k','l',       // home row
        'z','x','c','v','b','n','m'                // bottom row
    ];

    private static readonly char[] AlphabetOrder =
        "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    // ── Session state ─────────────────────────────────────────────────────────

    // Process name → assigned first letter
    private static readonly Dictionary<string, char> _processLetter = new();

    // HWND → assigned second-letter index into SecondChars
    private static readonly Dictionary<IntPtr, int> _hwndIndex = new();

    // Processes that have ever had more than one window — stays two-letter mode
    private static readonly HashSet<string> _multiWindow = new();

    // First letters already taken (across all processes)
    private static readonly HashSet<char> _usedLetters = new();

    // Next second-letter index per process
    private static readonly Dictionary<string, int> _nextIndex = new();

    public static void ClearSession()
    {
        _processLetter.Clear();
        _hwndIndex.Clear();
        _multiWindow.Clear();
        _usedLetters.Clear();
        _nextIndex.Clear();
    }

    // ── Main entry point ──────────────────────────────────────────────────────

    public static void AssignBindings(List<WindowInfo> windows, AppConfig config)
    {
        // Group by process
        var groups = windows.GroupBy(w => w.ProcessName).ToList();

        // Pass 1: ensure every process has a first letter
        foreach (var group in groups)
            EnsureProcessLetter(group.Key, config);

        // Pass 2: detect newly multi-window processes
        foreach (var group in groups)
        {
            if (group.Count() > 1)
                _multiWindow.Add(group.Key);
        }

        // Pass 3: assign bindings to each window
        foreach (var group in groups)
        {
            char letter = _processLetter[group.Key];
            bool isMulti = _multiWindow.Contains(group.Key);
            var groupWindows = group.ToList();

            if (!isMulti && groupWindows.Count == 1)
            {
                // Single-window mode: just the letter
                groupWindows[0].Binding = letter.ToString();
            }
            else
            {
                // Multi-window mode: assign stable second letters per HWND
                foreach (var w in groupWindows)
                {
                    if (!_hwndIndex.TryGetValue(w.Handle, out int idx))
                    {
                        idx = _nextIndex.GetValueOrDefault(group.Key, 0);
                        _hwndIndex[w.Handle] = idx;
                        _nextIndex[group.Key] = idx + 1;
                    }

                    char second = idx < SecondChars.Length
                        ? SecondChars[idx]
                        : (char)('0' + (idx - SecondChars.Length));

                    w.Binding = $"{letter}{second}";
                }
            }
        }

        // Pass 4: set display names and clean titles
        foreach (var group in groups)
        {
            string displayName = ResolveDisplayName(group.Key, config);
            foreach (var w in group)
            {
                w.DisplayName = displayName;
                w.CleanTitle = StripAppSuffix(w.Title, displayName);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnsureProcessLetter(string processName, AppConfig config)
    {
        if (_processLetter.ContainsKey(processName))
            return;

        char letter;

        // Configured binding takes priority
        if (config.Bindings.TryGetValue(processName, out string? configured)
            && configured.Length == 1
            && char.IsAsciiLetter(configured[0]))
        {
            letter = char.ToLower(configured[0]);
            // If somehow already taken (two configured apps with same letter), fall through
            if (!_usedLetters.Contains(letter))
            {
                _processLetter[processName] = letter;
                _usedLetters.Add(letter);
                return;
            }
        }

        // Auto-assign: first free letter from process name
        letter = AssignFirstLetter(processName, _usedLetters);
        _processLetter[processName] = letter;
        _usedLetters.Add(letter);
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

    private static string ResolveDisplayName(string processName, AppConfig config)
    {
        if (config.Names.TryGetValue(processName, out string? name) && !string.IsNullOrEmpty(name))
            return name;

        if (string.IsNullOrEmpty(processName)) return processName;
        return char.ToUpper(processName[0]) + processName[1..];
    }

    private static string StripAppSuffix(string title, string displayName)
    {
        if (string.IsNullOrEmpty(title)) return title;

        foreach (string sep in new[] { " \u2014 ", " - ", " | " })
        {
            int idx = title.LastIndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                string suffix = title[(idx + sep.Length)..].Trim();
                if (IsSimilar(suffix, displayName))
                    return title[..idx].Trim();
            }
        }

        return title;
    }

    private static bool IsSimilar(string suffix, string displayName)
    {
        if (string.IsNullOrEmpty(suffix) || string.IsNullOrEmpty(displayName))
            return false;

        suffix = suffix.ToLowerInvariant();
        displayName = displayName.ToLowerInvariant();

        if (suffix == displayName) return true;

        foreach (string word in displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length >= 3 && suffix.Contains(word))
                return true;
        }

        return false;
    }
}
