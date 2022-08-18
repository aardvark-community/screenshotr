global using Screenshotr;
global using System.Text.Json;
global using static System.Console;

namespace Screenshotr;

internal static class Helpers
{
    public static T[] Tail<T>(this T[] xs) => xs.Skip(1).ToArray();

    public static string[] ParseTags(string s)
    {
        if (s == null || s == "") return Array.Empty<string>();
        if (s[0] == '"') s = s[1..];
        if (s[^1] == '"') s = s[..^1];
        return s.Split(new char[] { ' ', '\t', ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    }
}