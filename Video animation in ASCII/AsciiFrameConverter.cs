namespace Video_animation_in_ASCII;

/// <summary>
/// Превращает 8-битный grayscale-кадр в готовую к выводу строку.
/// Яркость переводится в символ через заранее построенную таблицу на 256 значений.
/// </summary>
public sealed class AsciiFrameConverter
{
    private const string Gradient = " .,:;+*?%S#@";

    private readonly char[] _lut = new char[256];
    private readonly int _width;
    private readonly int _height;

    public AsciiFrameConverter(int width, int height)
    {
        _width = width;
        _height = height;
        for (int i = 0; i < _lut.Length; i++)
            _lut[i] = Gradient[i * Gradient.Length / 256];
    }

    public string Convert(byte[] pixels)
    {
        // ширина + перевод строки на каждую строку кадра
        return string.Create(_height * (_width + 1), (pixels, _lut, _width), static (chars, state) =>
        {
            var (pixels, lut, width) = state;
            int src = 0, dst = 0;
            while (src < pixels.Length)
            {
                for (int x = 0; x < width; x++)
                    chars[dst++] = lut[pixels[src++]];
                chars[dst++] = '\n';
            }
        });
    }
}
