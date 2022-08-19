using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Console;

var testfiles = new[]
{
    @"C:\Users\kellner\Videos\2022-08-10 12-52-35.mp4",
    @"C:\Users\kellner\Videos\VID_20220707_145855.mp4",
    @"C:\Users\kellner\Videos\2022-04-12 14-49-18.mp4",
    @"C:\Users\kellner\Videos\IMG_3126.MOV",
    @"C:\Users\kellner\Videos\IMG_3126.mp4",
    @"C:\Users\kellner\Pictures\IMG_20220212_084953.jpg",
    @"E:\_REPOS\masterlisa_master\src\MasterLisa.sln",
};

var sw = new Stopwatch();
foreach (var fn in testfiles)
{
    var isVideo = VideoUtils.IsVideo(fn);

    WriteLine(fn);
    WriteLine($"    is video         : {isVideo}");
    WriteLine($"    has displaymatrix: {VideoUtils.HasDisplayMatrix(fn)}");

    if (isVideo)
    {
        var info = VideoUtils.GetVideoInfo(fn);

        sw.Restart(); Write("converting video ... ");
        VideoUtils.Convert(fn, fn + ".converted.mp4", maxSize: 1280);
        WriteLine(sw.Elapsed);

        sw.Restart(); Write("creating thumbnail ... ");
        VideoUtils.CreateThumbnail(fn, fn + ".thumb.jpg", maxSize: 256);
        WriteLine(sw.Elapsed);
    }
}

public static class VideoUtils
{
    public static bool IsVideo(string filename)
    {
        var args = $"-v error -select_streams v:0 -show_entries stream=codec_type -of csv=p=0 \"{filename}\"";
        var lines = Execute("ffprobe", args);
        return lines.Any(line => line.Contains("video"));
    }

    public static bool HasDisplayMatrix(string filename)
    {
        var lines = Execute("ffprobe", $"\"{filename}\"");
        return lines.Any(line => line.Contains("displaymatrix"));
    }

    public static void CreateThumbnail(string filename, string outputFilename, int maxSize)
    {
        var (w, h) = ComputeScaleTargetSize(filename, maxSize);

        var tmpFilename = $"{outputFilename}_%d.jpg";
        var args = $"-i \"{filename}\" -vframes 1 -s {w}x{h} -f image2 \"{tmpFilename}\"";
        var _ = Execute("ffmpeg", args);
        tmpFilename = tmpFilename.Replace("%d", "1");
        File.Move(tmpFilename, outputFilename, overwrite: true);
    }

    public static void Convert(string filename, string outputFilename, int maxSize)
    {
        var (w, h) = ComputeScaleTargetSize(filename, maxSize);
        var args = $"-i \"{filename}\" -s {w}x{h} -y \"{outputFilename}\"";
        var _ = Execute("ffmpeg", args);
    }

    private static (int w, int h) ComputeScaleTargetSize(string filename, int max)
    {
        var info = GetVideoInfo(filename);
        var size = info.GetSize();

        if (size.Width <= max && size.Height <= max) 
            return (size.Width, size.Height);

        return size.Width > size.Height
            ? (max, (int)((double)max * size.Height / size.Width))
            : ((int)((double)max * size.Width / size.Height), max)
            ;
    }

    public record SideData(int? Rotation, string? DisplayMatrix)
    {
        [JsonPropertyName("side_data_type")]
        public string SideDataType { get; init; } = string.Empty;
    }
    public record VideoStream(int Index, int? Width, int? Height)
    {
        [JsonPropertyName("codec_name")]
        public string CodecName { get; init; } = string.Empty;

        [JsonPropertyName("codec_long_name")]
        public string CodecLongName { get; init; } = string.Empty;

        [JsonPropertyName("codec_type")]
        public string CodecType { get; init; } = string.Empty;

        [JsonPropertyName("side_data_list")]
        public SideData[]? SideDataList { get; init; } = null;
    }
    public record VideoInfo(VideoStream[] Streams)
    {
        public Size GetSize()
        {
            var (videoStream, rotation) = Streams
                .Where(x => x.CodecType == "video")
                .Select(x => (
                    videoStream: x,
                    rotation: x.SideDataList?.FirstOrDefault(y => y.Rotation != null)?.Rotation ?? 0
                    ))
                .FirstOrDefault()
                ;

            if (videoStream == null) throw new Exception("Missing video stream.");

            var (w, h) = (
                videoStream.Width ?? throw new Exception("Missing width."),
                videoStream.Height ?? throw new Exception("Missing height.")
                );

            var flip = rotation % 180 != 0;
            return flip ? new(h, w) : new(w, h);
        }
    }

    public static VideoInfo GetVideoInfo(string filename)
    {
        var args = $"-v quiet -print_format json -show_format -show_streams \"{filename}\"";
        var lines = Execute("ffprobe", args);
        var json = JsonSerializer.Deserialize<VideoInfo>(string.Join('\n', lines), _jsonOptions);
        return json!;
    }

    private static JsonSerializerOptions _jsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private static IEnumerable<string> Execute(string exe, string args, bool debug = false)
    {
        WriteLine($"[Execute] {exe} {args}");

        var processInfo = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
            //WorkingDirectory = @"C:\tmp"
        };

        using var process = Process.Start(processInfo) ?? throw new Exception("What?");

        var lines = new List<string>();
        process.OutputDataReceived += (e, args) =>
        {
            WriteLine($"[stdout] {args.Data}");
            if (args.Data != null) lines.Add(args.Data);
        }; 
        process.ErrorDataReceived += (e, args) =>
        {
            WriteLine($"[stderr] {args.Data}");
            WriteLine(args.Data);
            if (args.Data != null) lines.Add(args.Data);
        };
        //var stdout = debug ? "" : process.StandardOutput.ReadToEnd();
        //var stderr = debug ? "" : process.StandardError.ReadToEnd();
        process.BeginErrorReadLine(); process.BeginOutputReadLine();
        process.WaitForExit();

        //IEnumerable<string> split(string s)
        //    => s.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        ////var lines = split(stdout).Concat(split(stderr));
        return lines;
    }
}
