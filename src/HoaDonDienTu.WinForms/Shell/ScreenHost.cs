using System.Windows.Forms;

namespace HoaDonDienTu.WinForms.Shell
{
    /// <summary>Giữ đúng một screen trong vùng nội dung; dispose screen cũ trước khi nhận screen mới.</summary>
    public sealed class ScreenHost
    {
        private readonly Control _container;

        public ScreenHost(Control container)
        {
            _container = container;
        }

        public Control Current { get; private set; }

        public void Show(Control screen)
        {
            var previous = Current;
            _container.SuspendLayout();
            try
            {
                if (previous != null)
                {
                    _container.Controls.Remove(previous);
                    previous.Dispose();
                }

                screen.Dock = DockStyle.Fill;
                _container.Controls.Add(screen);
                Current = screen;
            }
            finally
            {
                _container.ResumeLayout(true);
            }
        }
    }
}
