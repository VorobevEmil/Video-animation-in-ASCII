using System.Diagnostics;
using System.Globalization;

namespace Video_animation_in_ASCII;

/// <summary>
/// Декодирует видео через ffmpeg: масштабирование и перевод в оттенки серого
/// выполняет сам ffmpeg, наружу отдаются сырые 8-битные кадры (1 байт на пиксель).
/// </summary>
public sealed class FfmpegVideoReader
{
    private static readonly string[] SearchDirs = ["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin"];

    public required string Path { get; init; }
    public double FrameRate { get; private set; }
    public long FrameCount { get; private set; }
    public int SourceWidth { get; private set; }
    public int SourceHeight { get; private set; }

    public void Probe()
    {
        string output = Run(FindTool("ffprobe"),
        [
            "-v", "error",
            "-select_streams", "v:0",
            "-count_packets",
            "-show_entries", "stream=width,height,r_frame_rate,nb_read_packets",
            "-of", "csv=p=0",
            Path
        ]);

        string[] parts = output.Trim().Split(',');
        if (parts.Length < 4)
            throw new InvalidOperationException($"Не удалось получить параметры видео: {output}");

        SourceWidth = int.Parse(parts[0], CultureInfo.InvariantCulture);
        SourceHeight = int.Parse(parts[1], CultureInfo.InvariantCulture);
        FrameRate = ParseFrameRate(parts[2]);
        FrameCount = long.Parse(parts[3], CultureInfo.InvariantCulture);
    }

    /// <summary>Читает кадры, отмасштабированные до width x height, в оттенках серого.</summary>
    public IEnumerable<byte[]> ReadFrames(int width, int height)
    {
        using var process = Start(FindTool("ffmpeg"),
        [
            "-v", "error",
            "-i", Path,
            "-vf", $"scale={width}:{height}:flags=area,format=gray",
            "-f", "rawvideo",
            "-pix_fmt", "gray",
            "pipe:1"
        ]);

        var stream = process.StandardOutput.BaseStream;
        int frameSize = width * height;
        while (true)
        {
            var frame = new byte[frameSize];
            if (!TryReadExactly(stream, frame))
                yield break;
            yield return frame;
        }
    }

    private static bool TryReadExactly(Stream stream, byte[] buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = stream.Read(buffer, read, buffer.Length - read);
            if (n == 0)
                return read == 0 ? false : throw new EndOfStreamException("Кадр оборван на середине.");
            read += n;
        }
        return true;
    }

    private static double ParseFrameRate(string ratio)
    {
        string[] parts = ratio.Split('/');
        double num = double.Parse(parts[0], CultureInfo.InvariantCulture);
        double den = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 1;
        return num / den;
    }

    private static string FindTool(string name)
    {
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator).Concat(SearchDirs))
        {
            string candidate = System.IO.Path.Combine(dir, name);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException($"Не найден {name}. Установите ffmpeg (например, `brew install ffmpeg`).");
    }

    private static Process Start(string tool, string[] args)
    {
        var startInfo = new ProcessStartInfo(tool)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Не удалось запустить {tool}.");
    }

    private static string Run(string tool, string[] args)
    {
        using var process = Start(tool, args);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{System.IO.Path.GetFileName(tool)} завершился с ошибкой: {error.Trim()}");
        return output;
    }
}
