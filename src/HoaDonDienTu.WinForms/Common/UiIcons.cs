using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Shell;

namespace HoaDonDienTu.WinForms.Common
{
    public enum UiIcon
    {
        Dashboard, Download, Archive, History, Building, Gear,
        Excel, Zip, CheckFiles, Clear, Search, Refresh, Back, Close, Save, Folder, Backup, Restore, Pdf, Print, View,
        Login, Switch, Pause, Play, Retry, Add, Prev, Next, Info, Key, Calendar, Purchase, Sold, Power, Link, Check, Trash
    }

    /// <summary>
    /// Bộ biểu tượng nét đơn sắc dùng cho nút và ribbon, vẽ bằng GDI+ trên lưới 20x20 nên đổi được màu và kích thước, không cần file ảnh.
    /// <see cref="Decorate"/> gắn icon cho các nút của một màn hình theo tên điều khiển.
    /// </summary>
    public static class UiIcons
    {
        public const int ButtonSize = 16;
        public static readonly Color Normal = Color.FromArgb(45, 62, 84);
        public static readonly Color Disabled = Color.FromArgb(160, 168, 178);
        public static readonly Color Accent = Color.FromArgb(31, 111, 178);

        /// <summary>Tên điều khiển → icon. Một tên có cùng ý nghĩa ở mọi màn hình (btnBack luôn là quay lại, btnPrev là trang trước...).</summary>
        private static readonly Dictionary<string, UiIcon> ByName = new Dictionary<string, UiIcon>(StringComparer.Ordinal)
        {
            // Kho hóa đơn, lịch sử, chi tiết, trình xem
            { "btnCheckFiles", UiIcon.CheckFiles }, { "btnExportExcel", UiIcon.Excel }, { "btnExportZip", UiIcon.Zip },
            { "btnPurchase", UiIcon.Purchase }, { "btnSold", UiIcon.Sold }, { "btnClear", UiIcon.Clear },
            { "btnPrev", UiIcon.Prev }, { "btnNext", UiIcon.Next },
            { "btnClose", UiIcon.Close }, { "btnCleanTemps", UiIcon.Trash },
            { "btnView", UiIcon.View }, { "btnBack", UiIcon.Back }, { "btnPrint", UiIcon.Print },
            { "btnSaveCopy", UiIcon.Save }, { "btnSavePdf", UiIcon.Pdf },

            // Doanh nghiệp
            { "btnPrimary", UiIcon.Save }, { "btnCancel", UiIcon.Close }, { "btnBrowse", UiIcon.Folder },
            { "btnSearch", UiIcon.Search }, { "btnAddCompany", UiIcon.Add },

            // Tổng quan, tải
            { "btnNewDownload", UiIcon.Download }, { "btnSession", UiIcon.Login }, { "btnOpenArchive", UiIcon.Archive },
            { "btnClearFilters", UiIcon.Clear }, { "rdoPurchase", UiIcon.Purchase }, { "rdoSold", UiIcon.Sold },
            { "btnKindMonth", UiIcon.Calendar }, { "btnKindQuarter", UiIcon.Calendar }, { "btnKindYear", UiIcon.Calendar }, { "btnKindCustom", UiIcon.Calendar },
            { "btnStartDownload", UiIcon.Download }, { "btnPause", UiIcon.Pause }, { "btnResume", UiIcon.Play }, { "btnRetryFailed", UiIcon.Retry },
            { "btnNewSession", UiIcon.Add },

            // Bản quyền, đăng nhập, đóng ứng dụng
            { "btnDeactivate", UiIcon.Power }, { "btnValidate", UiIcon.Check }, { "btnActivate", UiIcon.Key },
            { "btnRetry", UiIcon.Retry },
            { "btnWait", UiIcon.History }, { "btnCancelJob", UiIcon.Close }, { "btnStay", UiIcon.Back },

            // Cài đặt: các mục bên trái
            { "btnSecCompany", UiIcon.Building }, { "btnSecStorage", UiIcon.Archive }, { "btnSecPortal", UiIcon.Link },
            { "btnSecDiagnostics", UiIcon.Info }, { "btnSecLicense", UiIcon.Key }, { "btnSecAbout", UiIcon.Info },

            // Khung chính
            { "btnCompanyContext", UiIcon.Building }, { "btnPortalLogin", UiIcon.Login }
        };

        /// <summary>Nút thao tác của mục Cài đặt theo mã hành động.</summary>
        private static readonly Dictionary<string, UiIcon> ByAction = new Dictionary<string, UiIcon>(StringComparer.Ordinal)
        {
            { "open-folder", UiIcon.Folder }, { "backup", UiIcon.Backup }, { "restore", UiIcon.Restore }, { "refresh", UiIcon.Refresh },
            { "change-folder", UiIcon.Switch }, { "check-connection", UiIcon.Link }, { "cancel-operation", UiIcon.Close }
        };

        public static bool TryGetForName(string controlName, out UiIcon icon)
        {
            return ByName.TryGetValue(controlName ?? string.Empty, out icon);
        }

        public static bool TryGetForAction(string actionId, out UiIcon icon)
        {
            return ByAction.TryGetValue(actionId ?? string.Empty, out icon);
        }

        public static Bitmap Create(UiIcon icon, int size, Color color)
        {
            var bitmap = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bitmap))
            using (var pen = new Pen(color, 1.7F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(size / 20F, size / 20F);
                Draw(g, icon, pen, brush);
            }

            return bitmap;
        }

        /// <summary>Gắn icon cho một nút Krypton (ảnh thường và ảnh khi bị khóa); ảnh được giải phóng cùng nút.</summary>
        public static void Apply(KryptonButton button, UiIcon icon)
        {
            var normal = Create(icon, ButtonSize, Normal);
            var disabled = Create(icon, ButtonSize, Disabled);
            button.Values.Image = normal;
            button.Values.ImageStates.ImageDisabled = disabled;
            button.StateCommon.Content.AdjacentGap = 6;
            button.Disposed += delegate
            {
                normal.Dispose();
                disabled.Dispose();
            };
        }

        public static void Apply(KryptonCheckButton button, UiIcon icon)
        {
            var normal = Create(icon, ButtonSize, Normal);
            var disabled = Create(icon, ButtonSize, Disabled);
            button.Values.Image = normal;
            button.Values.ImageStates.ImageDisabled = disabled;
            button.StateCommon.Content.AdjacentGap = 6;
            button.Disposed += delegate
            {
                normal.Dispose();
                disabled.Dispose();
            };
        }

        /// <summary>Khoảng cách chuẩn giữa hai nút đứng cạnh nhau (px).</summary>
        public const int ButtonGap = 8;

        /// <summary>
        /// Gắn icon cho mọi nút có tên trong bảng ánh xạ ở <paramref name="root"/> và các điều khiển con (nút đã có ảnh được giữ nguyên),
        /// rồi chèn khoảng cách chuẩn giữa các nút xếp sát nhau bằng Dock.
        /// </summary>
        public static void Decorate(Control root)
        {
            ApplyIcons(root);
            SpaceDockedButtons(root);
        }

        /// <summary>
        /// Nút xếp bằng Dock Trái/Phải liền nhau thì dính vào nhau (Dock không dùng Margin). Chèn một dải trong suốt rộng <see cref="ButtonGap"/>
        /// giữa mỗi cặp. Điều khiển được xếp theo thứ tự ngược chỉ số: chỉ số cao nằm ngoài cùng, nên dải chèn ngay sau chỉ số của nút.
        /// </summary>
        public static void SpaceDockedButtons(Control root)
        {
            foreach (var side in new[] { DockStyle.Left, DockStyle.Right })
            {
                var buttons = new List<Control>();
                foreach (Control child in root.Controls)
                {
                    if (child.Dock == side && (child is KryptonButton || child is KryptonCheckButton)) buttons.Add(child);
                }

                // Từ nút trong cùng tới nút ngoài cùng; nút ngoài cùng không cần dải phía sau.
                buttons.Sort((a, b) => root.Controls.GetChildIndex(a).CompareTo(root.Controls.GetChildIndex(b)));
                for (var i = 0; i < buttons.Count - 1; i++)
                {
                    var spacer = new Panel { Dock = side, Width = ButtonGap, BackColor = Color.Transparent, TabStop = false, Name = "spacerBetweenButtons" };
                    root.Controls.Add(spacer);
                    root.Controls.SetChildIndex(spacer, root.Controls.GetChildIndex(buttons[i]) + 1);
                }
            }

            // Chỉ duyệt các điều khiển gốc; dải vừa chèn không có điều khiển con.
            foreach (Control child in new List<Control>(root.Controls.Cast<Control>()))
            {
                if (child.HasChildren) SpaceDockedButtons(child);
            }
        }

        private static void ApplyIcons(Control root)
        {
            foreach (Control child in root.Controls)
            {
                UiIcon icon;
                var button = child as KryptonButton;
                var check = child as KryptonCheckButton;
                if (button != null && button.Values.Image == null && TryGetForName(button.Name, out icon)) Apply(button, icon);
                else if (check != null && check.Values.Image == null && TryGetForName(check.Name, out icon)) Apply(check, icon);
                if (child.HasChildren) ApplyIcons(child);
            }
        }

        // ---- Vẽ trên lưới 20x20 ----

        private static void Draw(Graphics g, UiIcon icon, Pen pen, Brush brush)
        {
            switch (icon)
            {
                case UiIcon.Dashboard: NavigationIcons.Draw(g, ScreenId.Dashboard, pen, brush); break;
                case UiIcon.Download:
                case UiIcon.Restore: NavigationIcons.Draw(g, ScreenId.Download, pen, brush); break;
                case UiIcon.Archive: NavigationIcons.Draw(g, ScreenId.InvoiceArchive, pen, brush); break;
                case UiIcon.History: NavigationIcons.Draw(g, ScreenId.DownloadHistory, pen, brush); break;
                case UiIcon.Building: NavigationIcons.Draw(g, ScreenId.CompanyList, pen, brush); break;
                case UiIcon.Gear: NavigationIcons.Draw(g, ScreenId.Settings, pen, brush); break;

                case UiIcon.Excel:
                    g.DrawRectangle(pen, 3, 2.5F, 14, 15);
                    g.DrawLine(pen, 3, 7.5F, 17, 7.5F);
                    g.DrawLine(pen, 3, 12.5F, 17, 12.5F);
                    g.DrawLine(pen, 8.5F, 7.5F, 8.5F, 17.5F);
                    break;
                case UiIcon.Zip:
                    g.DrawRectangle(pen, 4.5F, 2.5F, 11, 15);
                    for (var y = 3.5F; y < 11; y += 2.5F) g.FillRectangle(brush, 9, y, 2, 1.4F);
                    g.DrawRectangle(pen, 8.5F, 11.5F, 3, 3.5F);
                    break;
                case UiIcon.CheckFiles:
                    g.DrawPolygon(pen, new[] { new PointF(10, 2), new PointF(16.5F, 4.5F), new PointF(16.5F, 10), new PointF(10, 18), new PointF(3.5F, 10), new PointF(3.5F, 4.5F) });
                    g.DrawLines(pen, new[] { new PointF(6.8F, 9.5F), new PointF(9.2F, 12), new PointF(13.2F, 7) });
                    break;
                case UiIcon.Clear:
                    g.DrawPolygon(pen, new[] { new PointF(3, 3.5F), new PointF(17, 3.5F), new PointF(11.5F, 10), new PointF(11.5F, 16.5F), new PointF(8.5F, 14.5F), new PointF(8.5F, 10) });
                    break;
                case UiIcon.Search:
                    g.DrawEllipse(pen, 3, 3, 10, 10);
                    g.DrawLine(pen, 11.5F, 11.5F, 17, 17);
                    break;
                case UiIcon.Refresh:
                case UiIcon.Retry:
                    g.DrawArc(pen, 3.5F, 3.5F, 13, 13, 40, 280);
                    g.DrawLines(pen, new[] { new PointF(15.6F, 2.4F), new PointF(15.2F, 6.2F), new PointF(11.5F, 5.6F) });
                    break;
                case UiIcon.Back:
                    g.DrawLine(pen, 16, 10, 4.5F, 10);
                    g.DrawLines(pen, new[] { new PointF(9.5F, 4.8F), new PointF(4.5F, 10), new PointF(9.5F, 15.2F) });
                    break;
                case UiIcon.Close:
                    g.DrawLine(pen, 5, 5, 15, 15);
                    g.DrawLine(pen, 15, 5, 5, 15);
                    break;
                case UiIcon.Save:
                    g.DrawPolygon(pen, new[] { new PointF(3.5F, 3), new PointF(14.5F, 3), new PointF(16.5F, 5), new PointF(16.5F, 17), new PointF(3.5F, 17) });
                    g.DrawRectangle(pen, 6, 3, 7, 4.5F);
                    g.DrawRectangle(pen, 6, 11, 8, 6);
                    break;
                case UiIcon.Folder:
                    g.DrawPolygon(pen, new[] { new PointF(2.5F, 5), new PointF(8, 5), new PointF(9.5F, 7), new PointF(17.5F, 7), new PointF(17.5F, 16.5F), new PointF(2.5F, 16.5F) });
                    break;
                case UiIcon.Backup:
                    g.DrawLines(pen, new[] { new PointF(3, 13), new PointF(3, 17), new PointF(17, 17), new PointF(17, 13) });
                    g.DrawLine(pen, 10, 13, 10, 3);
                    g.DrawLines(pen, new[] { new PointF(5.5F, 7.5F), new PointF(10, 3), new PointF(14.5F, 7.5F) });
                    break;
                case UiIcon.Pdf:
                    g.DrawPolygon(pen, new[] { new PointF(4.5F, 2), new PointF(12, 2), new PointF(15.5F, 5.5F), new PointF(15.5F, 18), new PointF(4.5F, 18) });
                    g.DrawLines(pen, new[] { new PointF(12, 2), new PointF(12, 5.5F), new PointF(15.5F, 5.5F) });
                    g.DrawLine(pen, 7, 10, 13, 10);
                    g.DrawLine(pen, 7, 13.5F, 13, 13.5F);
                    break;
                case UiIcon.Print:
                    g.DrawRectangle(pen, 5.5F, 2.5F, 9, 4.5F);
                    g.DrawRectangle(pen, 2.5F, 7, 15, 7);
                    g.DrawRectangle(pen, 5.5F, 11.5F, 9, 6);
                    break;
                case UiIcon.View:
                    g.DrawClosedCurve(pen, new[] { new PointF(2, 10), new PointF(6, 5.5F), new PointF(10, 4.2F), new PointF(14, 5.5F), new PointF(18, 10), new PointF(14, 14.5F), new PointF(10, 15.8F), new PointF(6, 14.5F) }, 0.5F, FillMode.Alternate);
                    g.FillEllipse(brush, 8, 8, 4, 4);
                    break;
                case UiIcon.Login:
                    g.DrawLines(pen, new[] { new PointF(9, 3), new PointF(16.5F, 3), new PointF(16.5F, 17), new PointF(9, 17) });
                    g.DrawLine(pen, 3, 10, 12, 10);
                    g.DrawLines(pen, new[] { new PointF(8.2F, 6.2F), new PointF(12, 10), new PointF(8.2F, 13.8F) });
                    break;
                case UiIcon.Switch:
                    g.DrawLine(pen, 3, 7, 16, 7);
                    g.DrawLines(pen, new[] { new PointF(12.5F, 3.8F), new PointF(16, 7), new PointF(12.5F, 10.2F) });
                    g.DrawLine(pen, 17, 13, 4, 13);
                    g.DrawLines(pen, new[] { new PointF(7.5F, 9.8F), new PointF(4, 13), new PointF(7.5F, 16.2F) });
                    break;
                case UiIcon.Pause:
                    g.FillRectangle(brush, 5, 3.5F, 3.5F, 13);
                    g.FillRectangle(brush, 11.5F, 3.5F, 3.5F, 13);
                    break;
                case UiIcon.Play:
                    g.FillPolygon(brush, new[] { new PointF(6, 3.5F), new PointF(16, 10), new PointF(6, 16.5F) });
                    break;
                case UiIcon.Add:
                    g.DrawLine(pen, 10, 4, 10, 16);
                    g.DrawLine(pen, 4, 10, 16, 10);
                    break;
                case UiIcon.Prev:
                    g.DrawLines(pen, new[] { new PointF(12.5F, 3.5F), new PointF(6, 10), new PointF(12.5F, 16.5F) });
                    break;
                case UiIcon.Next:
                    g.DrawLines(pen, new[] { new PointF(7.5F, 3.5F), new PointF(14, 10), new PointF(7.5F, 16.5F) });
                    break;
                case UiIcon.Info:
                    g.DrawEllipse(pen, 3, 3, 14, 14);
                    g.DrawLine(pen, 10, 9, 10, 14);
                    g.FillEllipse(brush, 9, 5.6F, 2, 2);
                    break;
                case UiIcon.Key:
                    g.DrawEllipse(pen, 2.5F, 6, 7, 7);
                    g.DrawLine(pen, 9.5F, 9.5F, 17.5F, 9.5F);
                    g.DrawLine(pen, 14.5F, 9.5F, 14.5F, 13);
                    g.DrawLine(pen, 17.5F, 9.5F, 17.5F, 12.5F);
                    break;
                case UiIcon.Calendar:
                    g.DrawRectangle(pen, 3, 4.5F, 14, 12.5F);
                    g.DrawLine(pen, 3, 8.5F, 17, 8.5F);
                    g.DrawLine(pen, 7, 2.5F, 7, 6);
                    g.DrawLine(pen, 13, 2.5F, 13, 6);
                    g.FillRectangle(brush, 6, 11, 2, 2);
                    g.FillRectangle(brush, 9, 11, 2, 2);
                    g.FillRectangle(brush, 12, 11, 2, 2);
                    break;
                case UiIcon.Purchase:
                    g.DrawRectangle(pen, 3.5F, 7, 13, 10.5F);
                    g.DrawArc(pen, 6.5F, 2.5F, 7, 9, 180, 180);
                    break;
                case UiIcon.Sold:
                    g.DrawPolygon(pen, new[] { new PointF(3, 3), new PointF(10, 3), new PointF(17, 10), new PointF(10, 17), new PointF(3, 10) });
                    g.FillEllipse(brush, 5.8F, 5.8F, 2.6F, 2.6F);
                    break;
                case UiIcon.Power:
                    g.DrawArc(pen, 3.5F, 4.5F, 13, 13, -50, 280);
                    g.DrawLine(pen, 10, 2.5F, 10, 9.5F);
                    break;
                case UiIcon.Link:
                    g.DrawEllipse(pen, 3, 3, 14, 14);
                    g.DrawLine(pen, 3, 10, 17, 10);
                    g.DrawEllipse(pen, 7, 3, 6, 14);
                    break;
                case UiIcon.Check:
                    g.DrawEllipse(pen, 3, 3, 14, 14);
                    g.DrawLines(pen, new[] { new PointF(6.5F, 10), new PointF(9, 12.5F), new PointF(13.5F, 7.5F) });
                    break;
                case UiIcon.Trash:
                    g.DrawLine(pen, 3.5F, 5.5F, 16.5F, 5.5F);
                    g.DrawLines(pen, new[] { new PointF(8, 5.5F), new PointF(8, 3), new PointF(12, 3), new PointF(12, 5.5F) });
                    g.DrawLines(pen, new[] { new PointF(5, 5.5F), new PointF(6, 17.5F), new PointF(14, 17.5F), new PointF(15, 5.5F) });
                    g.DrawLine(pen, 8.5F, 8.5F, 8.5F, 14.5F);
                    g.DrawLine(pen, 11.5F, 8.5F, 11.5F, 14.5F);
                    break;
            }
        }
    }
}
