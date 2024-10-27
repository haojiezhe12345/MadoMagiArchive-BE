using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SkiaSharp;
using MadoMagiArchive.DataServices;

namespace MadoMagiArchive.FileServices
{
    public partial class MediaInfo
    {
        public class StreamInfo
        {
            public required string codec_type { get; set; }
            public int? width { get; set; }
            public int? height { get; set; }
            public string? duration { get; set; }
            public StreamTags? tags { get; set; }
        }

        public class StreamTags
        {
            public string? DURATION { get; set; }
        }

        public required List<StreamInfo> streams { get; set; }

        public StreamInfo? GetFirstVideoStream() => streams.FirstOrDefault(x => x.codec_type == "video");

        [JsonIgnore]
        public int? Width => GetFirstVideoStream()?.width;

        [JsonIgnore]
        public int? Height => GetFirstVideoStream()?.height;

        [JsonIgnore]
        public double? Duration
        {
            get
            {
                var stream = GetFirstVideoStream();
                if (stream?.duration != null)
                    return Double.Parse(stream.duration);
                else if (stream?.tags?.DURATION != null)
                {
                    var duration = TimeSpan.ParseExact(DurationDigitTrimmer.Trim(stream.tags.DURATION), "g", null);
                    return duration.TotalSeconds;
                }
                else return null;
            }
        }

        public static partial class DurationDigitTrimmer
        {
            [GeneratedRegex(@"(\.\d{7})\d+")]
            private static partial Regex Regex();
            public static string Trim(string duration) => Regex().Replace(duration, "$1");
        }
    }

    public class FFmpeg
    {
        public static async Task<MediaInfo?> GetMediaInfo(string file)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -of json -show_streams \"{file}\"",
                    RedirectStandardOutput = true,
                });
                if (process == null) return null;

                using var reader = process.StandardOutput;
                var output = await reader.ReadToEndAsync();
                await process.WaitForExitAsync();

                return JsonSerializer.Deserialize<MediaInfo>(output);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<int?> CreateVideoThumb(string inFile, string outFile, int maxWidth, int maxHeight)
        {
            try
            {
                using var process = Process.Start(
                    "ffmpeg",
                    $"-i \"{inFile}\" -vf \"thumbnail,scale='if(gt(a,{maxWidth}/{maxHeight}),{maxWidth},-1)':'if(gt(a,{maxWidth}/{maxHeight}),-1,{maxHeight})'\" -vframes 1 \"{outFile}\""
                );
                await process.WaitForExitAsync();
                return process.ExitCode;
            }
            catch
            {
                return null;
            }
        }
    }

    public static class FileItemMediaExtensions
    {
        public static bool IsImage(this FileItem fileItem) => fileItem.Type?.StartsWith("image") ?? false;
        public static bool IsVideo(this FileItem fileItem) => fileItem.Type?.StartsWith("video") ?? false;

        public static void AddImageInfo(this FileItem fileItem, FileStream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
            {
                fileItem.Width = bitmap.Width;
                fileItem.Height = bitmap.Height;
            }
        }

        public static async Task AddImageInfo(this FileItem fileItem, string filePath)
        {
            var imageInfo = await FFmpeg.GetMediaInfo(filePath);
            if (imageInfo != null)
            {
                fileItem.Width = imageInfo.Width;
                fileItem.Height = imageInfo.Height;
            }
        }

        public static async Task AddVideoInfo(this FileItem fileItem, string filePath)
        {
            var videoInfo = await FFmpeg.GetMediaInfo(filePath);
            if (videoInfo != null)
            {
                fileItem.Width = videoInfo.Width;
                fileItem.Height = videoInfo.Height;
                fileItem.Duration = videoInfo.Duration;
            }
        }
    }
}
