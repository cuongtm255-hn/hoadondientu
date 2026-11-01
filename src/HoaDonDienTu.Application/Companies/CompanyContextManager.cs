using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Companies
{
    public interface ICompanyContextAccessor
    {
        bool HasCurrent { get; }
        CompanyContext Current { get; }
    }

    /// <summary>Chỉ interface này được thay đổi context hiện tại.</summary>
    public interface ICompanyContextManager : ICompanyContextAccessor
    {
        event EventHandler ContextChanged;

        /// <exception cref="CompanyStorageException">Kho của MST không mở được.</exception>
        Task<CompanyContext> OpenAsync(CompanyProfile target, CancellationToken cancellationToken);

        void CloseCurrent();
    }

    /// <summary>Kiểm tra và mở kho (thư mục + company.db) của một MST; implementation ở DD-02/DD-06.</summary>
    public interface ICompanyStorageOpener
    {
        /// <exception cref="CompanyStorageException"/>
        Task<CompanyContext> OpenAsync(CompanyProfile company, CancellationToken cancellationToken);
    }

    public sealed class CompanyStorageException : Exception
    {
        public CompanyStorageException(CompanySwitchBlocker blocker, string message)
            : base(message)
        {
            Blocker = blocker;
        }

        public CompanySwitchBlocker Blocker { get; private set; }
    }

    public sealed class CompanyContextManager : ICompanyContextManager
    {
        private readonly ICompanyStorageOpener _opener;

        public CompanyContextManager(ICompanyStorageOpener opener)
        {
            if (opener == null) throw new ArgumentNullException("opener");
            _opener = opener;
        }

        public event EventHandler ContextChanged;

        public bool HasCurrent
        {
            get { return Current != null; }
        }

        public CompanyContext Current { get; private set; }

        public async Task<CompanyContext> OpenAsync(CompanyProfile target, CancellationToken cancellationToken)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (Current != null) throw new InvalidOperationException("Phải đóng context hiện tại trước khi mở context mới.");

            var context = await _opener.OpenAsync(target, cancellationToken).ConfigureAwait(false);

            // Chỉ cập nhật context sau khi mở thành công.
            Current = context;
            RaiseChanged();
            return context;
        }

        public void CloseCurrent()
        {
            var previous = Current;
            if (previous == null) return;
            Current = null;
            previous.Dispose();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            var handler = ContextChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
