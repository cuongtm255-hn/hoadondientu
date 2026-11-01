using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.Navigation
{
    public sealed class NavigationResult
    {
        private NavigationResult(bool succeeded, ScreenId current, string reason)
        {
            Succeeded = succeeded;
            Current = current;
            Reason = reason;
        }

        public bool Succeeded { get; private set; }
        public ScreenId Current { get; private set; }
        public string Reason { get; private set; }

        public static NavigationResult Success(ScreenId current)
        {
            return new NavigationResult(true, current, null);
        }

        public static NavigationResult Blocked(ScreenId current, string reason)
        {
            return new NavigationResult(false, current, reason);
        }
    }

    public interface INavigationService
    {
        ScreenId CurrentScreen { get; }
        bool CanNavigate(ScreenId target);

        Task<NavigationResult> NavigateAsync(
            ScreenId target,
            object parameter,
            CancellationToken cancellationToken);
    }
}
