using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace InstallKit.WixSharp
{
    /// <summary>
    /// Generates the two bitmaps every WiX/WixSharp dialog set uses, in the product's colours:
    /// the 493×58 top banner and the 493×312 welcome/finish background. Flat, no gradients, so the
    /// stock dialogs stop looking like 2005.
    /// </summary>
    internal static class BrandingImages
    {
        const int Width = 493;
        const int BannerHeight = 58;
        const int DialogHeight = 312;
        const int SidePanelWidth = 164; // the WixUI welcome text starts right of this

        public static (string Banner, string Background) Generate(string productName, Branding branding, string outputDirectory, string fileStem)
        {
            Directory.CreateDirectory(outputDirectory);
            var accent = ColorTranslator.FromHtml(string.IsNullOrWhiteSpace(branding.AccentColor) ? Branding.DefaultAccentColor : branding.AccentColor!);
            using var logo = LoadLogo(branding.LogoFile);

            var banner = Path.Combine(outputDirectory, fileStem + ".banner.bmp");
            using (var bitmap = new Bitmap(Width, BannerHeight))
            using (var g = Prepare(bitmap))
            {
                g.Clear(Color.White);
                using (var bar = new SolidBrush(accent))
                    g.FillRectangle(bar, 0, BannerHeight - 3, Width, 3);

                if (logo != null)
                    DrawFitted(g, logo, new Rectangle(Width - 8 - 44, 5, 44, 44));

                bitmap.Save(banner, ImageFormat.Bmp);
            }

            var background = Path.Combine(outputDirectory, fileStem + ".dialog.bmp");
            using (var bitmap = new Bitmap(Width, DialogHeight))
            using (var g = Prepare(bitmap))
            {
                g.Clear(Color.White);
                using (var panel = new SolidBrush(accent))
                    g.FillRectangle(panel, 0, 0, SidePanelWidth, DialogHeight);

                if (logo != null)
                {
                    DrawFitted(g, logo, new Rectangle(32, 40, SidePanelWidth - 64, SidePanelWidth - 64));
                }
                else
                {
                    using var font = new Font("Segoe UI Semibold", 13f, GraphicsUnit.Point);
                    using var text = new SolidBrush(ReadableOn(accent));
                    g.DrawString(productName, font, text, new RectangleF(16, 40, SidePanelWidth - 32, 120));
                }

                bitmap.Save(background, ImageFormat.Bmp);
            }

            return (banner, background);
        }

        /// <summary>A copy of the logo as .png for the bundle UI, which does not accept every format.</summary>
        public static string? LogoAsPng(Branding branding, string outputDirectory, string fileStem)
        {
            if (string.IsNullOrWhiteSpace(branding.LogoFile))
                return null;

            if (string.Equals(Path.GetExtension(branding.LogoFile), ".png", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(branding.LogoFile);

            using var logo = LoadLogo(branding.LogoFile)!;
            var path = Path.Combine(outputDirectory, fileStem + ".logo.png");
            Directory.CreateDirectory(outputDirectory);
            logo.Save(path, ImageFormat.Png);
            return path;
        }

        static Image? LoadLogo(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                using var icon = new Icon(path, 256, 256);
                return icon.ToBitmap();
            }

            // Copy so the file is not kept locked.
            using var source = Image.FromFile(path);
            return new Bitmap(source);
        }

        static Graphics Prepare(Bitmap bitmap)
        {
            var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            return g;
        }

        static void DrawFitted(Graphics g, Image image, Rectangle box)
        {
            var scale = Math.Min((float)box.Width / image.Width, (float)box.Height / image.Height);
            var w = (int)(image.Width * scale);
            var h = (int)(image.Height * scale);
            g.DrawImage(image, box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }

        static Color ReadableOn(Color background)
        {
            var luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255;
            return luminance > 0.6 ? Color.FromArgb(32, 32, 32) : Color.White;
        }
    }
}
