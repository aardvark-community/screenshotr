using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Screenshotr;

public record Repository(string BaseDirectory, ImmutableDictionary<string, Screenshot> Entries)
{
    public static Repository Init(string baseDirectory)
    {
        if (!Directory.Exists(baseDirectory)) Directory.CreateDirectory(baseDirectory);

        var datadir = Path.Combine(baseDirectory, "data");
        if (!Directory.Exists(datadir)) Directory.CreateDirectory(datadir);

        var entries = LoadAndCacheEntries(datadir).ToImmutableDictionary(x => x.Id);

        return new(baseDirectory, entries);
    }

    private static IEnumerable<Screenshot> LoadAndCacheEntries(string datadir)
    {
        foreach (var yeardir in Directory.EnumerateDirectories(datadir, "*.*", SearchOption.TopDirectoryOnly))
        {
            foreach (var monthdir in Directory.EnumerateDirectories(yeardir, "*.*", SearchOption.TopDirectoryOnly))
            {
                foreach (var daydir in Directory.EnumerateDirectories(monthdir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var dayCacheFile = daydir + ".json";
                    if (File.Exists(dayCacheFile))
                    {
                        var json = File.ReadAllText(dayCacheFile);
                        var xs = JsonSerializer.Deserialize<Screenshot[]>(json, JsonOptions) ?? throw new Exception($"Failed to parse JSON {json}.");
                        foreach (var x in xs) yield return x;
                    }
                    else
                    {
                        var dayEntries = new List<Screenshot>();
                        foreach (var jsonfile in Directory.EnumerateFiles(daydir, "*.json", SearchOption.TopDirectoryOnly))
                        {
                            var x = Screenshot.ParseJson(File.ReadAllText(jsonfile));
                            yield return x;
                            dayEntries.Add(x);
                        }
                        var json = JsonSerializer.Serialize(dayEntries, JsonOptions);
                        File.WriteAllText(dayCacheFile, json);
                        Console.WriteLine($"[Repository] wrote {dayCacheFile} ({dayEntries.Count} entries)");
                    }
                }
            }
        }

        yield break;
    }

    public int Count => Entries.Count;

    public async Task<Repository> UpdateScreenshot(Screenshot screenshot)
    {
        var newSelf = this with { Entries = Entries.SetItem(screenshot.Id, screenshot) };

        var meta = screenshot.ToJson();
        await File.WriteAllTextAsync(Path.Combine(BaseDirectory, screenshot.RelPathMeta), meta);

        DeleteDayCacheFile(screenshot);

        return newSelf;
    }

    public async Task<(Repository Repository, Screenshot? Screenshot, bool IsDuplicate)> ImportScreenshot(
        byte[] buffer, 
        DateTimeOffset? timestamp, 
        IEnumerable<string> tags,
        Custom custom,
        ImportInfo importInfo
        )
    {
        try
        {
            timestamp ??= DateTimeOffset.Now;
            importInfo ??= ImportInfo.Now;

            var id = Convert.ToHexString(SHA1.Create().ComputeHash(buffer)).ToLower();
            if (Entries.ContainsKey(id)) return (this, Entries[id], true);

            var info = Image.Identify(buffer);
            if (info == null) return (this, null, false);
            var size = new V2i(info.Width, info.Height);

            var screenshot = new Screenshot(
                Id: id, 
                Created: timestamp.Value,
                Bytes: buffer.Length, 
                Size: size, 
                Tags: tags.ToImmutableHashSet(),
                Custom: custom,
                importInfo
                );

            var pathFullRes = Path.Combine(BaseDirectory, screenshot.RelPathFullRes);
            Directory.CreateDirectory(Path.GetDirectoryName(pathFullRes)!);
            await File.WriteAllBytesAsync(pathFullRes, buffer);

            var pathThumb = Path.Combine(BaseDirectory, screenshot.RelPathThumb);
            using var img = Image.Load<Rgba32>(buffer);
            using var thumb = Resize(img, new Size(256, 256));
            //await thumb.SaveAsJpegAsync(pathThumb, new JpegEncoder() { ColorType = JpegColorType.Rgb, Quality = 80 }); ;
            await thumb.SaveAsPngAsync(pathThumb, new PngEncoder() { ColorType = PngColorType.RgbWithAlpha });

            var pathMeta = Path.Combine(BaseDirectory, screenshot.RelPathMeta);
            var meta = screenshot.ToJson();
            await File.WriteAllTextAsync(pathMeta, meta);

            DeleteDayCacheFile(screenshot);

            var newSelf = this with { Entries = Entries.Add(screenshot.Id, screenshot) };
            return (newSelf, screenshot, false);
        }
        catch
        {
            return (this, null, false);
        }
    }

    private void DeleteDayCacheFile(Screenshot x)
    {
        var filename = Path.GetDirectoryName(Path.Combine(BaseDirectory, x.RelPathMeta)) + ".json";
        if (File.Exists(filename)) File.Delete(filename);
    }

    private static Image<Rgba32> Resize(Image<Rgba32> img, Size size)
    {
        return img
            .Clone(context => context
                .BackgroundColor(new Rgba32(255, 255, 255, 0))
                .Resize(new ResizeOptions
                {
                    Size = size,
                    Mode = ResizeMode.Pad,
                    PremultiplyAlpha = false
                })
            );
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        IgnoreReadOnlyFields = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };
}