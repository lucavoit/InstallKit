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
    /// the 493×58 top banner and the 493×312 welcome/finish background, plus a 493×360 full-page background for
    /// the modern pages. In the dark theme the pages are near-black with a soft glow of the accent colour; the
    /// stock dialogs that reuse the first two keep dark text on a light body, so those stay light with a dark side panel.
    /// </summary>
    internal static class BrandingImages
    {
        const int Width = 493;
        const int BannerHeight = 58;
        const int DialogHeight = 312;
        const int PageHeight = 360;      // the 370x270 dialog units of a modern page, buttons included
        const int SidePanelWidth = 164; // the WixUI welcome text starts right of this

        public static (string Banner, string Background, string Page) Generate(string productName, Branding branding, string outputDirectory, string fileStem)
        {
            Directory.CreateDirectory(outputDirectory);
            var accent = ColorTranslator.FromHtml(string.IsNullOrWhiteSpace(branding.AccentColor) ? Branding.DefaultAccentColor : branding.AccentColor!);
            using var logo = LoadLogo(branding.LogoFile);
            var dark = branding.Theme == InstallerTheme.Dark;

            var banner = Path.Combine(outputDirectory, fileStem + ".banner.bmp");
            using (var bitmap = new Bitmap(Width, BannerHeight))
            using (var g = Prepare(bitmap))
            {
                g.Clear(Color.White);
                using (var bar = new LinearGradientBrush(new Rectangle(0, BannerHeight - 3, Width, 3), accent, Mix(accent, Color.White, 0.45), LinearGradientMode.Horizontal))
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
                if (dark)
                    PaintDark(g, accent, new Rectangle(0, 0, SidePanelWidth, DialogHeight));
                else
                    using (var panel = new SolidBrush(accent))
                        g.FillRectangle(panel, 0, 0, SidePanelWidth, DialogHeight);

                if (logo != null)
                {
                    DrawFitted(g, logo, new Rectangle(32, 40, SidePanelWidth - 64, SidePanelWidth - 64));
                }
                else
                {
                    using var font = new Font("Segoe UI Semibold", 13f, GraphicsUnit.Point);
                    using var text = new SolidBrush(dark ? Color.FromArgb(243, 244, 246) : ReadableOn(accent));
                    g.DrawString(productName, font, text, new RectangleF(16, 40, SidePanelWidth - 32, 120));
                }

                bitmap.Save(background, ImageFormat.Bmp);
            }

            var page = Path.Combine(outputDirectory, fileStem + ".page.bmp");
            using (var bitmap = new Bitmap(Width, PageHeight))
            using (var g = Prepare(bitmap))
            {
                var all = new Rectangle(0, 0, Width, PageHeight);
                if (dark)
                {
                    PaintDark(g, accent, all);
                    // A slightly lifted card on the left holds the logo; the right side stays calm for the text.
                    using var card = new SolidBrush(Color.FromArgb(34, 255, 255, 255));
                    using var path = RoundedRectangle(new Rectangle(20, 24, SidePanelWidth - 40, PageHeight - 78), 14);
                    g.FillPath(card, path);
                    using var edge = new Pen(Color.FromArgb(40, 255, 255, 255));
                    g.DrawPath(edge, path);
                }
                else
                {
                    g.Clear(Color.White);
                    using var panel = new SolidBrush(accent);
                    g.FillRectangle(panel, 0, 0, SidePanelWidth, PageHeight);
                }

                var box = new Rectangle(36, 44, SidePanelWidth - 72, SidePanelWidth - 72);
                if (logo != null)
                {
                    DrawFitted(g, logo, box);
                }
                else
                {
                    using var font = new Font("Segoe UI Semibold", 14f, GraphicsUnit.Point);
                    using var text = new SolidBrush(dark ? Color.FromArgb(243, 244, 246) : ReadableOn(accent));
                    g.DrawString(productName, font, text, new RectangleF(32, 44, SidePanelWidth - 64, 160));
                }

                bitmap.Save(page, ImageFormat.Bmp);
            }

            return (banner, background, page);
        }

        /// <summary>Near-black surface with a soft glow of the accent in the top left corner and a faint one at the bottom.</summary>
        static void PaintDark(Graphics g, Color accent, Rectangle area)
        {
            var baseColor = Color.FromArgb(15, 17, 22);
            using (var fill = new LinearGradientBrush(area, Mix(baseColor, accent, 0.06), baseColor, LinearGradientMode.Vertical))
                g.FillRectangle(fill, area);

            var state = g.Save();
            g.SetClip(area);
            Glow(g, accent, new Point(area.Left + 20, area.Top + 10), (int)(area.Width * 0.9), 120);
            Glow(g, accent, new Point(area.Left + area.Width / 2, area.Bottom + 20), (int)(area.Width * 0.7), 50);
            g.Restore(state);
        }

        static void Glow(Graphics g, Color accent, Point centre, int radius, int alpha)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(centre.X - radius, centre.Y - radius, radius * 2, radius * 2);
            using var brush = new PathGradientBrush(path)
            {
                CenterPoint = centre,
                CenterColor = Color.FromArgb(alpha, accent),
                SurroundColors = new[] { Color.FromArgb(0, accent) },
            };
            g.FillPath(brush, path);
        }

        static Color Mix(Color a, Color b, double amount) => Color.FromArgb(
            (int)(a.R + (b.R - a.R) * amount), (int)(a.G + (b.G - a.G) * amount), (int)(a.B + (b.B - a.B) * amount));

        static GraphicsPath RoundedRectangle(Rectangle r, int radius)
        {
            var d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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
