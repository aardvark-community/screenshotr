using System;
using System.IO;

namespace Screenshotr;

public record V2i(int X, int Y);

public record ImportInfo(string Username, string Hostname, string Process, string OsVersion, string ClrVersion)
{
    public static ImportInfo Now => new(
        Username: Environment.UserName,
        Hostname: Environment.MachineName,
        Process: Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess()?.MainModule?.FileName) ?? "",
        OsVersion: Environment.OSVersion.ToString(),
        ClrVersion: Environment.Version.ToString()
        );
}