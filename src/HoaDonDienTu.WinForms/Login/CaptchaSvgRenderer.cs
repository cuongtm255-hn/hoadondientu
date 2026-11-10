using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>
    /// Vẽ ảnh CAPTCHA SVG của cổng Thuế (api-samples mục 0) ra bitmap để hiển thị trong form đăng nhập riêng. Ảnh thật chỉ dùng các thẻ
    /// <c>&lt;path&gt;</c> với lệnh M, L, H, V, C, Q, Z (chữ vẽ thành đường viền tô màu, thêm vài đường nhiễu chỉ có nét). Gặp lệnh khác
    /// (A, S, T...) hoặc dữ liệu hỏng thì ném <see cref="FormatException"/> thay vì vẽ sai, để form báo "bấm Đổi mã".
    /// Không chạy script, không tải tài nguyên ngoài: chỉ đọc chuỗi SVG.
    /// </summary>
    public static class CaptchaSvgRenderer
    {
        /// <summary>Chặn SVG lớn bất thường (ảnh thật khoảng vài chục KB).</summary>
        public const int MaxSvgLength = 200000;

        private static readonly Regex PathTag = new Regex(@"<path\b([^>]*?)/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Attribute = new Regex(@"([\w:-]+)\s*=\s*""([^""]*)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RootSize = new Regex(@"<svg\b[^>]*?\bwidth\s*=\s*""([\d.]+)""[^>]*?\bheight\s*=\s*""([\d.]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Token = new Regex(@"[MmLlHhVvCcQqZz]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static Bitmap Render(string svg, int pixelWidth, int pixelHeight)
        {
            if (string.IsNullOrWhiteSpace(svg) || svg.Length > MaxSvgLength || svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new FormatException("Ảnh CAPTCHA không phải SVG hợp lệ.");
            }

            if (pixelWidth <= 0 || pixelHeight <= 0) throw new ArgumentOutOfRangeException("pixelWidth");

            float width = 200, height = 40;
            var size = RootSize.Match(svg);
            if (size.Success)
            {
                width = ParseFloat(size.Groups[1].Value);
                height = ParseFloat(size.Groups[2].Value);
            }

            if (width <= 0 || height <= 0) throw new FormatException("Kích thước ảnh CAPTCHA không hợp lệ.");

            var shapes = new List<Shape>();
            foreach (Match tag in PathTag.Matches(svg))
            {
                var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match attribute in Attribute.Matches(tag.Groups[1].Value))
                {
                    attributes[attribute.Groups[1].Value] = attribute.Groups[2].Value;
                }

                string d;
                if (!attributes.TryGetValue("d", out d)) continue;

                string fill;
                string stroke;
                attributes.TryGetValue("fill", out fill);
                attributes.TryGetValue("stroke", out stroke);
                shapes.Add(new Shape(BuildPath(d), ParseColor(fill, true), ParseColor(stroke, false)));
            }

            if (shapes.Count == 0) throw new FormatException("Ảnh CAPTCHA không có nét vẽ nào.");

            var bitmap = new Bitmap(pixelWidth, pixelHeight);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.ScaleTransform(pixelWidth / width, pixelHeight / height);
                    foreach (var shape in shapes)
                    {
                        using (shape.Path)
                        {
                            if (shape.Fill.HasValue)
                            {
                                using (var brush = new SolidBrush(shape.Fill.Value)) graphics.FillPath(brush, shape.Path);
                            }

                            if (shape.Stroke.HasValue)
                            {
                                using (var pen = new Pen(shape.Stroke.Value, 1f)) graphics.DrawPath(pen, shape.Path);
                            }
                        }
                    }
                }

                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        private sealed class Shape
        {
            public Shape(GraphicsPath path, Color? fill, Color? stroke)
            {
                Path = path;
                Fill = fill;
                Stroke = stroke;
            }

            public GraphicsPath Path { get; private set; }
            public Color? Fill { get; private set; }
            public Color? Stroke { get; private set; }
        }

        /// <summary>Thẻ không ghi fill thì SVG tô đen; không ghi stroke thì không vẽ nét.</summary>
        private static Color? ParseColor(string value, bool isFill)
        {
            if (string.IsNullOrEmpty(value)) return isFill ? Color.Black : (Color?)null;
            value = value.Trim();
            if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
            if (value.Length > 0 && value[0] == '#')
            {
                var hex = value.Substring(1);
                if (hex.Length == 3) hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
                int rgb;
                if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                {
                    return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                }
            }

            throw new FormatException("Màu không hỗ trợ trong ảnh CAPTCHA.");
        }

        private static GraphicsPath BuildPath(string d)
        {
            var path = new GraphicsPath(FillMode.Winding);
            var tokens = Token.Matches(d);
            var index = 0;
            char command = '\0';
            PointF current = PointF.Empty;
            PointF start = PointF.Empty;
            var figureOpen = false;

            Func<float> number = () =>
            {
                if (index >= tokens.Count) throw new FormatException("Dữ liệu đường vẽ bị thiếu số.");
                var text = tokens[index++].Value;
                if (char.IsLetter(text[0]) && text.Length == 1) throw new FormatException("Dữ liệu đường vẽ sai thứ tự.");
                return ParseFloat(text);
            };

            while (index < tokens.Count)
            {
                var text = tokens[index].Value;
                if (text.Length == 1 && char.IsLetter(text[0]))
                {
                    command = text[0];
                    index++;
                    if (command == 'Z' || command == 'z')
                    {
                        if (figureOpen) path.CloseFigure();
                        figureOpen = false;
                        current = start;
                        continue;
                    }
                }
                else if (command == '\0' || command == 'Z' || command == 'z')
                {
                    throw new FormatException("Dữ liệu đường vẽ không bắt đầu bằng lệnh.");
                }

                var relative = char.IsLower(command);
                var origin = relative ? current : PointF.Empty;
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                    {
                        var p = new PointF(origin.X + number(), origin.Y + number());
                        if (figureOpen) path.StartFigure();
                        current = p;
                        start = p;
                        figureOpen = true;
                        command = relative ? 'l' : 'L'; // các cặp số tiếp theo là LineTo
                        break;
                    }

                    case 'L':
                    {
                        var p = new PointF(origin.X + number(), origin.Y + number());
                        path.AddLine(current, p);
                        current = p;
                        break;
                    }

                    case 'H':
                    {
                        var p = new PointF((relative ? current.X : 0f) + number(), current.Y);
                        path.AddLine(current, p);
                        current = p;
                        break;
                    }

                    case 'V':
                    {
                        var p = new PointF(current.X, (relative ? current.Y : 0f) + number());
                        path.AddLine(current, p);
                        current = p;
                        break;
                    }

                    case 'C':
                    {
                        var c1 = new PointF(origin.X + number(), origin.Y + number());
                        var c2 = new PointF(origin.X + number(), origin.Y + number());
                        var p = new PointF(origin.X + number(), origin.Y + number());
                        path.AddBezier(current, c1, c2, p);
                        current = p;
                        break;
                    }

                    case 'Q':
                    {
                        var c = new PointF(origin.X + number(), origin.Y + number());
                        var p = new PointF(origin.X + number(), origin.Y + number());
                        // Bậc hai → bậc ba: c1 = p0 + 2/3 (c - p0), c2 = p + 2/3 (c - p).
                        var c1 = new PointF(current.X + 2f / 3f * (c.X - current.X), current.Y + 2f / 3f * (c.Y - current.Y));
                        var c2 = new PointF(p.X + 2f / 3f * (c.X - p.X), p.Y + 2f / 3f * (c.Y - p.Y));
                        path.AddBezier(current, c1, c2, p);
                        current = p;
                        break;
                    }

                    default:
                        throw new FormatException("Lệnh đường vẽ không hỗ trợ: " + command);
                }
            }

            return path;
        }

        private static float ParseFloat(string text)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) throw new FormatException("Số không hợp lệ trong ảnh CAPTCHA.");
            return value;
        }
    }
}
