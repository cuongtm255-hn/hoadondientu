using HoaDonDienTu.Application.Navigation;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class ScreenIdTests
    {
        [Test]
        public void Dashboard_IsDefined()
        {
            Assert.That(System.Enum.IsDefined(typeof(ScreenId), ScreenId.Dashboard), Is.True);
        }
    }
}
