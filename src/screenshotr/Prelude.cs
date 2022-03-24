global using Screenshotr;
global using Screenshotr.Cli;
global using System.Text.Json;
global using static System.Console;

namespace Screenshotr.Cli;

internal static class Utils
{
    public static T[] Tail<T>(this T[] xs) => xs.Skip(1).ToArray();

    public static string[] ParseTags(string s)
    {
        if (s == null || s == "") return Array.Empty<string>();
        if (s[0] == '"') s = s[1..];
        if (s[^1] == '"') s = s[..^1];
        return s.Split(new char[] { ' ', '\t', ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        IgnoreReadOnlyFields = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };
}
