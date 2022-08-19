using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Screenshotr;

public record CachedCredentials(string Endpoint, string Apikey);

public static class Roles
{
    public const string Admin    = "admin";
    public const string Importer = "importer";
}

public record ApiKey(string Description, string Key, DateTimeOffset Created, IReadOnlyList<string> Roles, DateTimeOffset ValidUntil, bool IsEnabled, bool IsDeletable)
{
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
    private static byte[] RandomBytes(int count)
    {
        var buffer = new byte[count]; 
        _rng.GetBytes(buffer);
        return buffer;
    }

#if DEBUG
    public static ApiKey Create(string description, string[] roles, DateTimeOffset validUntil, bool isEnabled, bool isDeletable, bool isEmptyDebugKey = false)
#else
    public static ApiKey Create(string description, string[] roles, DateTimeOffset validUntil, bool isEnabled = true, bool isDeletable = false)
#endif
    {
#if DEBUG
        var k = ToHexString(isEmptyDebugKey ? new byte[32] : RandomBytes(32));
#else
        var k = ToHexString(RandomBytes(32));
#endif

        var ak = new ApiKey(
            Description: description,
            Key: k,
            Created: DateTimeOffset.Now,
            Roles: roles,
            ValidUntil: validUntil,
            IsEnabled: isEnabled,
            IsDeletable: isDeletable
            );

        return ak;
    }

    public static string ToHexString(byte[] xs)
    {
        unsafe
        {
            var l = xs.Length << 1;
            var s = stackalloc sbyte[l | 1];
            s[l] = 0;
            var q = s;
            fixed (byte* _p = xs)
            {
                var p = _p;
                var end = p + xs.Length;
                while (p < end)
                {
                    var a = *p >> 4;
                    var b = *p & 0b1111;

                    *q++ = (sbyte)(a < 10 ? (byte)'0' + a : (byte)'a' + a - 10);
                    *q++ = (sbyte)(b < 10 ? (byte)'0' + b : (byte)'a' + b - 10);
                    p++;
                }
            }
            return new string(s);
        }
    }
}

public record ApiKeys(IImmutableDictionary<string, ApiKey> Keys)
{
    public static readonly ApiKeys Empty = new(ImmutableDictionary<string, ApiKey>.Empty);
    public int Count => Keys.Count;
    public ApiKeys Add(ApiKey k) => this with { Keys = Keys.Add(k.Key, k) };
    public ApiKeys Remove(ApiKey k) => k.IsDeletable ? this with { Keys = Keys.Remove(k.Key) } : this;
    public ApiKeys Remove(string h)
    {
        if (!Keys.TryGetValue(h, out var k)) return this;
        if (!k.IsDeletable) return this;
        return Remove(k);
    }

    /// <summary>
    /// Admin role includes ALL roles.
    /// </summary>
    public bool HasRole(string? authHeader, string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return true;
        if (authHeader == null || string.IsNullOrWhiteSpace(authHeader)) return false;
        if (!authHeader.StartsWith("Bearer ") == true) return false;

        if (Keys.TryGetValue(authHeader.Substring(7), out var apikey))
        {
            if (apikey.IsEnabled == false)                 return false;
            if (DateTimeOffset.UtcNow > apikey.ValidUntil) return false;
            if (apikey.Roles.Contains(Roles.Admin))        return true;
            if (apikey.Roles.Contains(role))               return true;
        }

        return false;
    }
}

public record ImgSize(int X, int Y)
{
    public static readonly ImgSize Unknown = new(-1, -1);
    public bool IsUnknown => X == -1 && Y == -1;
}

public record ImportInfo(
    string Username,
    string Hostname,
    string Process,
    string OsVersion,
    string ClrVersion,
    string? OriginalFileName
    )
{
    public static ImportInfo Now => new(
        Username: Environment.UserName,
        Hostname: Environment.MachineName,
        Process: Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess()?.MainModule?.FileName) ?? "",
        OsVersion: Environment.OSVersion.ToString(),
        ClrVersion: Environment.Version.ToString(),
        OriginalFileName: null
        );
}