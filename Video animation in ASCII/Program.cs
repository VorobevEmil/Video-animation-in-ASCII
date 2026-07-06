using System.Diagnostics;

namespace Video_animation_in_ASCII;

internal static class Program
{
    // символы в терминале выше, чем шире — сжимаем кадр по вертикали
    private const double HeightCompression = 2.0;
    private const int DefaultMaxWidth = 150;

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Использование: dotnet run -- <путь к видео> [ширина в символах]");
            return 1;
        }

        string path = args[0];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Файл не найден: {path}");
            return 1;
        }

        int maxWidth = DefaultMaxWidth;
        if (args.Length > 1 && (!int.TryParse(args[1], out maxWidth) || maxWidth <= 0))
        {
            Console.Error.WriteLine($"Некорректная ширина: {args[1]}");
            return 1;
        }

        try
        {
            Play(path, maxWidth);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
        finally
        {
            Console.CursorVisible = true;
            Console.ResetColor();
        }
    }

    private static void Play(string path, int maxWidth)
    {
        var reader = new FfmpegVideoReader { Path = path };
        reader.Probe();

        int width = Math.Min(maxWidth, reader.SourceWidth);
        int height = Math.Max(1, (int)(reader.SourceHeight / HeightCompression * width / reader.SourceWidth));
        var converter = new AsciiFrameConverter(width, height);

        Console.CancelKeyPress += (_, _) =>
        {
            Console.CursorVisible = true;
            Console.ResetColor();
        };
        Console.CursorVisible = false;

        // Рендер: ffmpeg отдаёт уже отмасштабированные grayscale-кадры
        var frames = new List<string>();
        foreach (byte[] frame in reader.ReadFrames(width, height))
        {
            frames.Add(converter.Convert(frame));
            if (frames.Count % 50 == 0 && reader.FrameCount > 0)
            {
                Console.SetCursorPosition(0, 0);
                Console.Write($"Рендер: {frames.Count * 100.0 / reader.FrameCount:0.0}% ({frames.Count}/{reader.FrameCount})");
            }
        }

        // Воспроизведение: темп держится по абсолютному времени, без накопления дрейфа
        Console.Clear();
        var output = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding, bufferSize: (width + 1) * height + 16)
        {
            AutoFlush = false,
        };
        double secondsPerFrame = 1.0 / reader.FrameRate;
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < frames.Count; i++)
        {
            Console.SetCursorPosition(0, 0);
            output.Write(frames[i]);
            output.Flush();

            var target = TimeSpan.FromSeconds((i + 1) * secondsPerFrame);
            var remaining = target - clock.Elapsed;
            if (remaining > TimeSpan.FromMilliseconds(2))
                Thread.Sleep(remaining - TimeSpan.FromMilliseconds(1));
            while (clock.Elapsed < target)
                Thread.SpinWait(64);
        }
    }
}
