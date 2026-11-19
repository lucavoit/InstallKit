using System;
using System.Globalization;

namespace InstallKit.WixSharp
{
    /// <summary>A colour as three bytes. Plain data, so the dialog and theme builders need no System.Drawing.</summary>
    internal readonly struct Rgb
    {
        public Rgb(int r, int g, int b)
        {
            R = r;
            G = g;
            B = b;
        }

        public int R { get; }

        public int G { get; }

        public int B { get; }

        /// <summary>Perceived brightness, 0 (black) to 1 (white).</summary>
        public double Luminance => (0.299 * R + 0.587 * G + 0.114 * B) / 255;

        /// <summary><c>RRGGBB</c>, the form the bundle theme uses.</summary>
        public string Hex => R.ToString("X2", CultureInfo.InvariantCulture) + G.ToString("X2", CultureInfo.InvariantCulture) + B.ToString("X2", CultureInfo.InvariantCulture);

        /// <summary>This colour moved <paramref name="amount"/> (0..1) towards <paramref name="other"/>.</summary>
        public Rgb Mix(Rgb other, double amount) => new Rgb(
            (int)Math.Round(R + (other.R - R) * amount), (int)Math.Round(G + (other.G - G) * amount), (int)Math.Round(B + (other.B - B) * amount));

        /// <summary>Reads <c>#RRGGBB</c>; anything else gives <paramref name="fallback"/>.</summary>
        public static Rgb Parse(string? text, Rgb fallback)
        {
            if (text == null || text.Length != 7 || text[0] != '#'
                || !int.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                return fallback;

            return new Rgb((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
        }
    }

    /// <summary>
    /// The colours of one installer: the theme's defaults with whatever the user chose on top. Secondary colours
    /// are derived from the chosen ones, so a custom background or text colour stays consistent.
    /// </summary>
    internal sealed class Palette
    {
        public static readonly Rgb DefaultAccent = new Rgb(0x00, 0x67, 0xC0);

        Palette(Rgb accent, Rgb background, Rgb text)
        {
            Accent = accent;
            Background = background;
            Text = text;
        }

        public Rgb Accent { get; }

        public Rgb Background { get; }

        public Rgb Text { get; }

        /// <summary>True when the background is dark, whichever theme asked for it.</summary>
        public bool IsDark => Background.Luminance < 0.5;

        /// <summary>Secondary text: the text colour faded into the background.</summary>
        public Rgb Muted => Text.Mix(Background, 0.45);

        /// <summary>A slightly lifted area (input boxes, cards): the background nudged towards the text colour.</summary>
        public Rgb Surface => Background.Mix(Text, IsDark ? 0.09 : 0.04);

        /// <summary>The accent as text on the background: lightened (or darkened) until it can be read.</summary>
        public Rgb Heading
        {
            get
            {
                var target = IsDark ? new Rgb(255, 255, 255) : new Rgb(0, 0, 0);
                var color = Accent;
                for (var i = 0; i < 4 && Math.Abs(color.Luminance - Background.Luminance) < 0.4; i++)
                    color = color.Mix(target, 0.25);
                return color;
            }
        }

        public static Palette Create(bool light, string? accent, string? background, string? text) => new Palette(
            Rgb.Parse(accent, DefaultAccent),
            Rgb.Parse(background, light ? new Rgb(255, 255, 255) : new Rgb(15, 17, 22)),
            Rgb.Parse(text, light ? new Rgb(24, 24, 27) : new Rgb(243, 244, 246)));
    }
}
