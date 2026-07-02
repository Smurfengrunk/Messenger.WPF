using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace Messenger
{
    /// <summary>
    /// BadgeIconFactory is a utility class that provides methods to create tray and overlay icons with badge counts for a messaging application.
    /// It generates icons with a red badge indicating the number of unread messages, supporting both tray and taskbar overlay contexts.
    /// </summary>
    internal static class BadgeIconFactory
    {
        private const int IconSize = 32;
        private static readonly Color BadgeColor = Color.FromArgb(232, 17, 35);

        public static IntPtr CreateTrayIcon(int count)
        {
            using var bitmap = CreateTransparentBitmap();
            using var graphics = CreateGraphics(bitmap);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            using var baseIcon = new Icon(iconPath, IconSize, IconSize);

            graphics.DrawIcon(baseIcon, new Rectangle(0, 0, IconSize, IconSize));
            DrawBadge(graphics, count, BadgeKind.Tray);

            return bitmap.GetHicon();
        }

        /// <summary>
        /// Creates a taskbar overlay icon with a badge count.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public static IntPtr CreateOverlayIcon(int count)
        {
            using var bitmap = CreateTransparentBitmap();
            using var graphics = CreateGraphics(bitmap);

            DrawBadge(graphics, count, BadgeKind.TaskbarOverlay);

            return bitmap.GetHicon();
        }

        /// <summary>
        /// Creates a transparent bitmap of the specified icon size.
        /// </summary>
        /// <returns></returns>
        private static Bitmap CreateTransparentBitmap()
        {
            var bitmap = new Bitmap(IconSize, IconSize, PixelFormat.Format32bppArgb);
            bitmap.SetResolution(96, 96);
            return bitmap;
        }

        /// <summary>
        /// Creates a Graphics object from the given bitmap with high-quality rendering settings.
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        private static Graphics CreateGraphics(Bitmap bitmap)
        {
            var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            return graphics;
        }

        /// <summary>
        /// Draws a badge with the specified count on the provided Graphics object, adjusting its appearance based on the BadgeKind (Tray or TaskbarOverlay).
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="count"></param>
        /// <param name="kind"></param>
        private static void DrawBadge(Graphics graphics, int count, BadgeKind kind)
        {
            var text = FormatCount(count);
            var bounds = kind == BadgeKind.TaskbarOverlay
                ? new RectangleF(1, 1, 30, 30)
                : GetTrayBadgeBounds(text);

            using var path = CreateRoundRect(bounds, bounds.Height / 2);
            using var shadowPath = CreateRoundRect(
                new RectangleF(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height),
                bounds.Height / 2);
            using var shadowBrush = new SolidBrush(Color.FromArgb(80, Color.Black));
            using var badgeBrush = new SolidBrush(BadgeColor);
            using var borderPen = new Pen(Color.White, kind == BadgeKind.TaskbarOverlay ? 2.2f : 1.8f);

            graphics.FillPath(shadowBrush, shadowPath);
            graphics.FillPath(badgeBrush, path);
            graphics.DrawPath(borderPen, path);

            var fontSize = kind == BadgeKind.TaskbarOverlay
                ? GetOverlayFontSize(text)
                : GetTrayFontSize(text);

            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.None
            };

            var textBounds = new RectangleF(bounds.X, bounds.Y - 1, bounds.Width, bounds.Height);
            graphics.DrawString(text, font, textBrush, textBounds, format);
        }

        /// <summary>
        /// Gets the bounds for the tray badge based on the length of the text. The badge size and position are adjusted to accommodate different text lengths.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static RectangleF GetTrayBadgeBounds(string text)
        {
            return text.Length switch
            {
                1 => new RectangleF(14, 13, 17, 17),
                2 => new RectangleF(10, 14, 21, 16),
                _ => new RectangleF(7, 14, 24, 16)
            };
        }

        /// <summary>
        /// Gets the appropriate font size for the tray badge based on the length of the text. The font size is adjusted to ensure that the text fits well within the badge.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static float GetTrayFontSize(string text)
        {
            return text.Length switch
            {
                1 => 12.5f,
                2 => 10.5f,
                _ => 8.5f
            };
        }

        /// <summary>
        /// Gets the appropriate font size for the taskbar overlay badge based on the length of the text. The font size is adjusted to ensure that the text fits well within the badge.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static float GetOverlayFontSize(string text)
        {
            return text.Length switch
            {
                1 => 20f,
                2 => 16.5f,
                _ => 12.5f
            };
        }

        /// <summary>
        /// Formats the count for display on the badge.
        /// If the count is zero or negative, it returns an empty string. If the count exceeds 99, it returns "99+" to indicate a large number of unread messages.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        private static string FormatCount(int count)
        {
            if (count <= 0)
                return string.Empty;

            return count > 99 ? "99+" : count.ToString();
        }

        /// <summary>
        /// Creates a rounded rectangle GraphicsPath based on the specified rectangle and corner radius. This method is used to draw the badge shape with rounded corners.
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        private static GraphicsPath CreateRoundRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        /// <summary>
        /// Enumeration to distinguish between the two types of badges: Tray and TaskbarOverlay. This helps in adjusting the badge's appearance and positioning based on its context.
        /// </summary>
        private enum BadgeKind
        {
            Tray,
            TaskbarOverlay
        }
    }
}
