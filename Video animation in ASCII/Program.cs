using System;
using Accord.Video.FFMPEG;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Video_animation_in_ASCII
{
    class Program
    {
        // compression
        private const double WIDTH_OFFSET = 1.5;
        // width
        private const int MAX_WIDTH = 150;
        private static double frameRate;

        static void Main(string[] args)
        {
            string path = @"C:\Users\vorob\Downloads\Bad Apple.mp4";

            Console.CursorVisible = false;
            Console.ForegroundColor = ConsoleColor.Red;
            VideoFileReader reader = new VideoFileReader();
            reader.Open(path);
            frameRate = reader.FrameRate.ToDouble();
            List<List<string>> result = new List<List<string>>();
            for (int i = 0; i < reader.FrameCount; i++)
            {
                Bitmap bitmap = reader.ReadVideoFrame();

                bitmap = ResizeBitmap(bitmap);
                bitmap.ToGrayscale();

                var converter = new BitmapToASCIIConverter(bitmap);
                result.Add(converter.Convert().Select(t => new string(t)).ToList());

                Console.Write($"Рендер видео завершен на {((i * 100) / (double)reader.FrameCount).ToString("0.00")}%");
                Console.SetCursorPosition(0, 0);

            }
            reader.Close();
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.White;


            foreach (var rows in result)
            {
                var frameTimeSW = Stopwatch.StartNew();
                foreach (var row in rows)
                    Console.WriteLine(row);
                double timeToSleep = ((1.0 / frameRate) * 1000.0);
                while (frameTimeSW.Elapsed.TotalMilliseconds < timeToSleep) ;
                Console.SetCursorPosition(0, 0);
                frameTimeSW.Stop();
            }
        }
        private static Bitmap ResizeBitmap(Bitmap bitmap)
        {
            var maxWidth = MAX_WIDTH;
            var maxHeight = bitmap.Height / WIDTH_OFFSET * maxWidth / bitmap.Width;
            if (bitmap.Width > maxWidth || bitmap.Height > maxHeight)
                bitmap = new Bitmap(bitmap, new Size(maxWidth, (int)maxHeight));
            return bitmap;
        }
    }
}
