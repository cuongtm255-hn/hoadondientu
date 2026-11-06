using System.Drawing;
using System.Drawing.Drawing2D;
using HoaDonDienTu.Application.Navigation;

namespace HoaDonDienTu.WinForms.Shell
{
    /// <summary>Biểu tượng nét đơn sắc cho menu trái, vẽ bằng GDI+ để không phụ thuộc file ảnh và đổi được màu theo trạng thái.</summary>
    public static class NavigationIcons
    {
        public const int Size = 20;

        public static Bitmap Create(ScreenId screen, Color color)
        {
            var bitmap = new Bitmap(Size, Size);
            using (var g = Graphics.FromImage(bitmap))
            using (var pen = new Pen(color, 1.8F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Draw(g, screen, pen, brush);
            }

            return bitmap;
        }

        /// <summary>Vẽ biểu tượng của một màn hình lên lưới 20x20 (người gọi có thể co giãn <paramref name="g"/> để ra kích thước khác).</summary>
        internal static void Draw(Graphics g, ScreenId screen, Pen pen, Brush brush)
        {
            switch (screen)
            {
                case ScreenId.Dashboard: Dashboard(g, brush); break;
                case ScreenId.Download: Download(g, pen); break;
                case ScreenId.InvoiceArchive: Archive(g, pen); break;
                case ScreenId.DownloadHistory: History(g, pen); break;
                case ScreenId.CompanyList: Building(g, pen, brush); break;
                case ScreenId.Settings: Gear(g, pen); break;
            }
        }

        private static void Dashboard(Graphics g, Brush brush)
        {
            g.FillRectangle(brush, 2, 2, 7, 7);
            g.FillRectangle(brush, 11, 2, 7, 5);
            g.FillRectangle(brush, 2, 11, 7, 5);
            g.FillRectangle(brush, 11, 9, 7, 7);
        }

        private static void Download(Graphics g, Pen pen)
        {
            g.DrawLine(pen, 10, 2, 10, 12);
            g.DrawLine(pen, 5.5F, 8, 10, 12.5F);
            g.DrawLine(pen, 14.5F, 8, 10, 12.5F);
            g.DrawLines(pen, new[] { new PointF(3, 13), new PointF(3, 17), new PointF(17, 17), new PointF(17, 13) });
        }

        private static void Archive(Graphics g, Pen pen)
        {
            g.DrawRectangle(pen, 2, 3, 16, 4);
            g.DrawLines(pen, new[] { new PointF(3.5F, 7), new PointF(3.5F, 17), new PointF(16.5F, 17), new PointF(16.5F, 7) });
            g.DrawLine(pen, 8, 11, 12, 11);
        }

        private static void History(Graphics g, Pen pen)
        {
            g.DrawEllipse(pen, 2.5F, 2.5F, 15, 15);
            g.DrawLines(pen, new[] { new PointF(10, 5.5F), new PointF(10, 10), new PointF(13.5F, 12) });
        }

        private static void Building(Graphics g, Pen pen, Brush brush)
        {
            g.DrawRectangle(pen, 4, 2.5F, 12, 15);
            g.FillRectangle(brush, 7, 5.5F, 2, 2);
            g.FillRectangle(brush, 11, 5.5F, 2, 2);
            g.FillRectangle(brush, 7, 9.5F, 2, 2);
            g.FillRectangle(brush, 11, 9.5F, 2, 2);
            g.DrawLine(pen, 10, 14, 10, 17.5F);
        }

        private static void Gear(Graphics g, Pen pen)
        {
            g.DrawEllipse(pen, 6, 6, 8, 8);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * System.Math.PI / 4;
                var cos = (float)System.Math.Cos(angle);
                var sin = (float)System.Math.Sin(angle);
                g.DrawLine(pen, 10 + cos * 6.5F, 10 + sin * 6.5F, 10 + cos * 9F, 10 + sin * 9F);
            }
        }
    }
}
