using System;
using System.Threading;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Chờ điều kiện bất đồng bộ trong test UI/presenter (thao tác chạy nền, kết quả về qua view giả).</summary>
    public static class UiWait
    {
        public static bool Until(Func<bool> condition, int timeoutMs = 10000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                if (condition()) return true;
                Thread.Sleep(15);
            }

            return condition();
        }
    }
}
