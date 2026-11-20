using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WixSharp;
using WixSharp.UI.Forms;
using WixSharp.UI.WPF;
using Wpf = System.Windows.Controls;

namespace InstallKit.WixSharp.Flat
{
    /// <summary>Names of the MSI properties and binaries the flat dialogs read at install time (written by <see cref="FlatUi"/>).</summary>
    internal static class FlatKeys
    {
        public const string Accent = "IK_FLAT_ACCENT";
        public const string Background = "IK_FLAT_BG";
        public const string Text = "IK_FLAT_TEXT";
        public const string Light = "IK_FLAT_LIGHT";

        /// <summary>The pages after the welcome page of an install: <c>license</c>, <c>settings</c> and/or <c>features</c>.</summary>
        public const string Pages = "IK_FLAT_PAGES";

        /// <summary>The prompted values: <c>PROPERTY|escaped label|required</c>, separated by <c>;</c>.</summary>
        public const string Prompts = "IK_FLAT_PROMPTS";

        /// <summary><c>1</c> when the product has a license of its own (WixSharp carries a placeholder one otherwise).</summary>
        public const string License = "IK_FLAT_LICENSE";

        public const string LogoBinary = "IK_Flat_Logo";
    }

    /// <summary>
    /// Base of the flat dialogs: the colours the user chose, one page layout (logo, big title, content, buttons at the bottom
    /// right) and a window caption in the page colour. The dialogs are plain WPF built in code, so the MSI needs no XAML resources.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class FlatDialog : WpfDialog, IWpfDialog
    {
        internal Palette Look;

        /// <summary>The heading of the page.</summary>
        protected TextBlock TitleBlock;

        /// <summary>The line under the heading.</summary>
        protected TextBlock SubtitleBlock;

        /// <summary>The area between the heading and the buttons.</summary>
        protected Grid Body;

        /// <summary>The buttons at the bottom right, in the order they are added (the last one is the right-most).</summary>
        protected StackPanel Buttons;

        /// <summary>A button at the left of the bottom row.</summary>
        protected StackPanel LeftButtons;

        public void Init()
        {
            var session = Session;
            // Everything the page needs from the MSI session is read here. Later (the window styling runs on the UI thread at idle
            // time) the session must not be touched: the install runs on another thread, and the two can block each other.
            // The last page, after the install, can no longer read the properties: it uses what the first page remembered.
            var light = session.Property(FlatKeys.Light);
            if (string.IsNullOrEmpty(light) && rememberedLook != null)
            {
                Look = rememberedLook;
                productName = rememberedProductName;
            }
            else
            {
                productName = session.Property("ProductName");
                Look = Palette.Create(IsLight(light), NullIfEmpty(session.Property(FlatKeys.Accent)),
                    NullIfEmpty(session.Property(FlatKeys.Background)), NullIfEmpty(session.Property(FlatKeys.Text)));
                rememberedLook = Look;
                rememberedProductName = productName;
            }

            Resources.MergedDictionaries.Add(FlatTheme.Create(Look));

            // The content stretches to whatever size the shell window has, and is laid out for the shell's usual size.
            Background = Paint("Bg");
            Content = BuildFrame();
            Build();

            // The caption of the window is styled once the page sits in it. WixSharp calls Init when the page is created, often
            // before that (the first page of a flow), so this keeps trying every 50 ms for a few seconds as well as on Loaded.
            Loaded += (sender, args) => StyleWindow();
            var attempts = 0;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (sender, args) =>
            {
                if (StyleWindow() || ++attempts >= 60)
                    timer.Stop();
            };
            timer.Start();
        }

        /// <summary>Fills in the heading, the body and the buttons.</summary>
        protected abstract void Build();

        internal ISession Session => ManagedFormHost.Runtime.Session;

        internal IManagedUIShell ShellApi => ManagedFormHost.Shell;

        internal Brush Paint(string key) => (Brush)Resources.MergedDictionaries[0][key];

        string productName;

        static Palette rememberedLook;
        static string rememberedProductName;

        internal string ProductName => productName ?? Session.Property("ProductName");

        internal string[] PagesAfterWelcome
        {
            get
            {
                var pages = Session.Property(FlatKeys.Pages);
                // "-" means none (an MSI property cannot be empty).
                return string.IsNullOrEmpty(pages) ? new string[0] : pages.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Where(page => page != "-").ToArray();
            }
        }

        // "1" light, "0" dark, "auto" what Windows is set to for apps (dark when it cannot be read).
        static bool IsLight(string setting)
        {
            if (setting == "1")
                return true;
            if (setting != "auto")
                return false;

            try
            {
                var value = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", null);
                return value is int light && light != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static string NullIfEmpty(string text) => string.IsNullOrWhiteSpace(text) ? null : text;

        /// <summary>A button added to the right-hand group. <paramref name="primary"/> draws it in the accent colour.</summary>
        protected Wpf.Button AddButton(string text, System.Action click, bool primary = false, bool left = false)
        {
            var button = new Wpf.Button { Content = text, Margin = new Thickness(left ? 0 : 10, 0, 0, 0) };
            if (primary)
            {
                button.Style = (Style)Resources.MergedDictionaries[0]["Primary"];
                button.IsDefault = true;
            }

            button.Click += (sender, args) => click();
            (left ? LeftButtons : Buttons).Children.Add(button);
            return button;
        }

        protected TextBlock Label(string text, bool muted = false, double size = 13)
        {
            var block = new TextBlock { Text = text, FontSize = size };
            if (muted)
                block.Foreground = Paint("Muted");
            return block;
        }

        FrameworkElement BuildFrame()
        {
            var root = new Grid { Background = Paint("Bg") };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(36, 22, 36, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var logo = LoadLogo();
            if (logo != null)
            {
                header.Children.Add(new Wpf.Image { Source = logo, Width = 56, Height = 56, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Top });
            }

            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TitleBlock = new TextBlock
            {
                FontSize = 28,
                FontWeight = FontWeights.SemiBold,
                Foreground = Paint("Text"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            };
            SubtitleBlock = new TextBlock { FontSize = 13, Foreground = Paint("Muted"), Margin = new Thickness(2, 4, 0, 0) };
            titles.Children.Add(TitleBlock);
            titles.Children.Add(SubtitleBlock);
            Grid.SetColumn(titles, 1);
            header.Children.Add(titles);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Body = new Grid { Margin = new Thickness(36, 6, 36, 4) };
            Grid.SetRow(Body, 1);
            root.Children.Add(Body);

            var footer = new Grid { Margin = new Thickness(36, 8, 36, 22) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            LeftButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            Buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(Buttons, 1);
            footer.Children.Add(LeftButtons);
            footer.Children.Add(Buttons);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            return root;
        }

        BitmapSource LoadLogo()
        {
            try
            {
                using (var bitmap = Session.GetResourceBitmap(FlatKeys.LogoBinary))
                {
                    if (bitmap == null)
                        return null;

                    var stream = new MemoryStream();
                    bitmap.Save(stream, ImageFormat.Png);
                    stream.Position = 0;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (Exception)
            {
                // No logo was given (or it cannot be read): the page just has no logo.
                return null;
            }
        }

        // The window is Windows' own frame. On Windows 11 its caption can follow the page colours, which makes the page look
        // borderless; the shell leaves the title empty, unlike the native dialogs. Returns false while there is no window yet.
        // Nothing here may touch the MSI session (see Init).
        bool StyleWindow()
        {
            try
            {
                var window = ShellWindow();
                if (window == IntPtr.Zero)
                    return false;

                SetWindowText(window, productName + " Setup");
                Set(window, 20 /* USE_IMMERSIVE_DARK_MODE */, Look.IsDark ? 1 : 0);
                Set(window, 35 /* CAPTION_COLOR */, ColorRef(Look.Background));
                Set(window, 34 /* BORDER_COLOR */, ColorRef(Look.Background));
                Set(window, 36 /* TEXT_COLOR */, ColorRef(Look.Text));
            }
            catch (Exception)
            {
                // An older Windows without these attributes keeps its standard caption.
            }

            return true;
        }

        // The setup window is the top-level window this page sits in: none until the page is hosted.
        IntPtr ShellWindow()
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            return source == null ? IntPtr.Zero : GetAncestor(source.Handle, 2 /* GA_ROOT */);
        }

        static int ColorRef(Rgb color) => color.R | (color.G << 8) | (color.B << 16);

        static void Set(IntPtr window, int attribute, int value) => DwmSetWindowAttribute(window, attribute, ref value, sizeof(int));

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetWindowText(IntPtr window, string text);
    }
}
