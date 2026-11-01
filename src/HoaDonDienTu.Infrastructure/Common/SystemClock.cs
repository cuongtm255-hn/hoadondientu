using System;
using HoaDonDienTu.Application.Abstractions;

namespace HoaDonDienTu.Infrastructure.Common
{
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }
}
