using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Screenshotr;

public static class Roles
{
    public const string Admin   = "admin";
    public const string Import  = "import";
}

public record ApiKey(string Description, string Hash, string Salt, DateTimeOffset Created, IReadOnlyList<string> Roles, DateTimeOffset ValidUntil, bool IsEnabled, bool IsDeletable)
{
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
    private static byte[] RandomBytes(int count)
    {
        var buffer = new byte[count]; 
        _rng.GetBytes(buffer);
        return buffer;
    }

#if DEBUG
    public static (ApiKey ApiKey, string ApiKeyClearText) Create(bool isEmptyDebugKey, string description, string[] roles, DateTimeOffset validUntil, bool isEnabled, bool isDeletable)
#else
    public static (ApiKey ApiKey, string ApiKeyClearText) Create(string description, string[] roles, DateTimeOffset validUntil, bool isEnabled = true, bool isDeletable = false)
#endif
    {
        // key
#if DEBUG
        var salt = ToHexString(isEmptyDebugKey ? new byte[16] : RandomBytes(16));
        var k = ToHexString(isEmptyDebugKey ? new byte[16] : RandomBytes(16));
#else
        var salt = ToHexString(RandomBytes(16));
        var k = ToHexString(RandomBytes(32));
#endif
        var kSalted = salt + k;
        var h = ToHexString(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(kSalted)));

        var ak = new ApiKey(
            Description: description,
            Hash: h,
            Salt: salt,
            Created: DateTimeOffset.Now,
            Roles: roles,
            ValidUntil: validUntil,
            IsEnabled: isEnabled,
            IsDeletable: isDeletable
            );

        return (ak, kSalted);
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
    public ApiKeys Add(ApiKey k) => this with { Keys = Keys.Add(k.Hash, k) };
    public ApiKeys Remove(string h) => this with { Keys = Keys.Remove(h) };
}

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