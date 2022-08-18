using FFMpegCore;
using Microsoft.AspNetCore.Components.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Screenshotr;

public record Repository
{
    public static Repository Init(string baseDirectory)
    {
        if (!Directory.Exists(baseDirectory)) Directory.CreateDirectory(baseDirectory);

        var dbdir = Path.Combine(baseDirectory, "db");
        if (!Directory.Exists(dbdir)) Directory.CreateDirectory(dbdir);
        var apiKeys = LoadApiKeysFile(Path.Combine(dbdir, "apikeys.json"));

        var datadir = Path.Combine(baseDirectory, "data");
        if (!Directory.Exists(datadir)) Directory.CreateDirectory(datadir);
        var entries = LoadAndCacheEntries(datadir).ToImmutableDictionary(x => x.Id);

        return new(baseDirectory, entries, apiKeys);
    }

    public string BaseDirectory { get; init; }
    public ImmutableDictionary<string, Screenshot> Entries { get; init; }
    public ApiKeys ApiKeys { get; init; }

    private Repository(string baseDirectory, ImmutableDictionary<string, Screenshot> entries, ApiKeys apiKeys)
    {
        BaseDirectory = baseDirectory;
        Entries = entries;
        ApiKeys = apiKeys;
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

            var mediaType = MediaType.Unknown;
            TimeSpan? videoDuration = null;

            ImgSize size;
            var info = Image.Identify(buffer);
            if (info == null)
            {
                // no image, but could be video
                var mediaInfo = await FFProbe.AnalyseAsync(new MemoryStream(buffer));
                Console.WriteLine(mediaInfo.ToJsonString());
                if (mediaInfo?.PrimaryVideoStream == null)
                {
                    return (this, null, false);
                }
                else
                {
                    // we have VIDEO
                    size = new ImgSize(
                        mediaInfo.PrimaryVideoStream.Width,
                        mediaInfo.PrimaryVideoStream.Height
                        );
                    videoDuration = mediaInfo.Duration;
                    mediaType = MediaType.Video;
                }
            }
            else
            {
                // we have IMAGE
                size = new ImgSize(info.Width, info.Height);
                mediaType = MediaType.Image;
            }

            var screenshot = new Screenshot(
                Id: id,
                Created: timestamp.Value,
                Bytes: buffer.Length,
                Size: size,
                Tags: tags.ToImmutableHashSet(),
                MediaType: mediaType,
                Custom: custom,
                importInfo
                );

            // store original media file
            var pathFullRes = Path.Combine(BaseDirectory, screenshot.RelPathFullRes);
            Directory.CreateDirectory(Path.GetDirectoryName(pathFullRes)!);
            await File.WriteAllBytesAsync(pathFullRes, buffer);

            // store thumbnail
            var pathThumb = Path.Combine(BaseDirectory, screenshot.RelPathThumb);
            switch (mediaType)
            {
                case MediaType.Image:
                    {
                        using var img = Image.Load<Rgba32>(buffer);
                        using var thumb = Resize(img, new Size(256, 256));
                        //await thumb.SaveAsJpegAsync(pathThumb, new JpegEncoder() { ColorType = JpegColorType.Rgb, Quality = 80 }); ;
                        await thumb.SaveAsPngAsync(pathThumb, new PngEncoder() { ColorType = PngColorType.RgbWithAlpha });

                        break;
                    }
                case MediaType.Video:
                    {
                        if (videoDuration > TimeSpan.FromSeconds(5))
                            videoDuration = TimeSpan.FromSeconds(5);
                        else
                            videoDuration = TimeSpan.Zero;

                        var (w,h) = size.X > size.Y
                            ? (256, (int)(256.0 * size.Y / size.X))
                            : ((int)(256.0 * size.X / size.Y), 256)
                            ;

                        FFMpeg.Snapshot(
                            pathFullRes,
                            pathThumb, 
                            new System.Drawing.Size(w, h),
                            videoDuration
                            );
                        break;
                    }

                default:
                    throw new Exception($"Unknown media type {mediaType}.");
            }
           
            // store metadata
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

    #region api keys

    private string? _apiKeysFileName;

    public string ApiKeysFileName
    {
        get
        {
            if (_apiKeysFileName == null) _apiKeysFileName = Path.Combine(BaseDirectory, "db", "apikeys.json");
            return _apiKeysFileName;
        }
    }

    private static ApiKeys LoadApiKeysFile(string filename)
    {
        if (!File.Exists(filename))
        {

#if DEBUG
            var globalAdmin = ApiKey.Create(isEmptyDebugKey: true, description: "global admin", roles: new[] { Roles.Admin }, validUntil: DateTimeOffset.MaxValue, isEnabled: true, isDeletable: false);
#else
            var globalAdmin = ApiKey.Create(description: "global admin", roles: new[] { Roles.Admin }, validUntil: DateTimeOffset.MaxValue, isEnabled: true );
#endif

            Console.WriteLine();
            Console.WriteLine(new string('=', 79));
            Console.WriteLine($"  ADMIN KEY: {globalAdmin.Key}");
            Console.WriteLine(new string('=', 79));
            Console.WriteLine();

            var x = ApiKeys.Empty.Add(globalAdmin);
            var s = JsonSerializer.Serialize(x, JsonOptions);
            File.WriteAllText(filename, s);
        }

        var json = File.ReadAllText(filename);
        var result = JsonSerializer.Deserialize<ApiKeys>(json, JsonOptions);
        if (result == null) throw new Exception("Failed to deserialize file: {filename}");
        return result;
    }

    public Repository SaveApiKeys()
    {
        var json = JsonSerializer.Serialize(ApiKeys, JsonOptions);
        Console.Write($"writing {ApiKeysFileName} ... ");
        File.WriteAllText(ApiKeysFileName, json);
        Console.WriteLine("done");
        return this;
    }

    #endregion

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