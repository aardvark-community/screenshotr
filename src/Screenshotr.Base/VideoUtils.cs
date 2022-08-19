using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screenshotr
{
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
            var info = GetVideoInfo(filename);
            var (w, h) = info.ComputeScaleTargetSize(maxSize);
            var ss = (int)Math.Min(10, info.GetDuration().TotalSeconds);

            var tmpFilename = $"{outputFilename}_%d.jpg";
            var args = $"-i \"{filename}\" -ss 00:00:{ss:00} -vframes 1 -s {w}x{h} -f image2 \"{tmpFilename}\"";
            var _ = Execute("ffmpeg", args);
            tmpFilename = tmpFilename.Replace("%d", "1");
            File.Move(tmpFilename, outputFilename, overwrite: true);
        }

        public static void Convert(string filename, string outputFilename, int maxSize)
        {
            var info = GetVideoInfo(filename);
            var (w, h) = info.ComputeScaleTargetSize(maxSize);
            var args = $"-i \"{filename}\" -s {w}x{h} -y \"{outputFilename}\"";
            var _ = Execute("ffmpeg", args);
        }

        

        public record SideData(int? Rotation, string? DisplayMatrix)
        {
            [JsonPropertyName("side_data_type")]
            public string SideDataType { get; init; } = string.Empty;
        }
        public record VideoStream(int Index, int? Width, int? Height, string? Duration)
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
        public record VideoInfo(
            string? Duration,
            VideoStream[] Streams,
            VideoStream? PrimaryVideoStream,
            VideoStream? PrimaryAudioStream
            )
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

            public TimeSpan GetDuration()
                => Duration switch
                {
                    null => TimeSpan.Zero,
                    _ when Duration.Contains(':') => TimeSpan.Parse(Duration),
                    _ => TimeSpan.FromSeconds(double.Parse(Duration, CultureInfo.InvariantCulture))
                };

            public (int w, int h) ComputeScaleTargetSize(int max)
            {
                var size = GetSize();

                if (size.Width <= max && size.Height <= max)
                    return (size.Width, size.Height);

                return size.Width > size.Height
                    ? (max, (int)((double)max * size.Height / size.Width))
                    : ((int)((double)max * size.Width / size.Height), max)
                    ;
            }
        }

        public static VideoInfo GetVideoInfo(string filename)
        {
            var args = $"-v quiet -print_format json -show_format -show_streams \"{filename}\"";
            var lines = Execute("ffprobe", args);
            var jsonString = string.Join('\n', lines);
            var json = JsonSerializer.Deserialize<VideoInfo>(jsonString, _jsonOptions);
            return json!;
        }

        private static JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static IEnumerable<string> Execute(string exe, string args, bool debug = false)
        {
            Console.WriteLine($"[Execute] {exe} {args}");

            var processInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(processInfo) ?? throw new Exception("What?");

            var lines = new List<string>();
            process.OutputDataReceived += (e, args) =>
            {
                Console.WriteLine($"[stdout] {args.Data}");
                if (args.Data != null) lines.Add(args.Data);
            };
            process.ErrorDataReceived += (e, args) =>
            {
                Console.WriteLine($"[stderr] {args.Data}");
                Console.WriteLine(args.Data);
                if (args.Data != null) lines.Add(args.Data);
            };

            process.BeginErrorReadLine(); process.BeginOutputReadLine();
            process.WaitForExit();

            return lines;
        }
    }

}
