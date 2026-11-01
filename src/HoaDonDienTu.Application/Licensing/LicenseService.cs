using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    /// <summary>Kích hoạt, xác thực và hủy kích hoạt licence theo DD-03 mục 4.</summary>
    public sealed class LicenseService : ILicenseService
    {
        private readonly ILicenseStore _store;
        private readonly ILicenseServer _server;
        private readonly IMachineIdentity _machine;
        private readonly ILicenseSignatureVerifier _verifier;
        private readonly IClock _clock;
        private readonly IApplicationLogger _logger;
        private readonly string _appVersion;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private LicenseInfo _info = LicenseInfo.NotActivated;

        public LicenseService(
            ILicenseStore store,
            ILicenseServer server,
            IMachineIdentity machine,
            ILicenseSignatureVerifier verifier,
            IClock clock,
            IApplicationLogger logger,
            string appVersion)
        {
            _store = store;
            _server = server;
            _machine = machine;
            _verifier = verifier;
            _clock = clock;
            _logger = logger;
            _appVersion = appVersion ?? string.Empty;
        }

        public event EventHandler InfoChanged;

        public LicenseInfo CurrentInfo
        {
            get { return _info; }
        }

        public async Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return Publish(EvaluateStored(true)).Status;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var load = SafeLoad();
                if (load.Status != LicenseLoadStatus.Ok) return Publish(EvaluateStored(false)).Status;

                var stored = load.License;
                var machine = _machine.GetMachineIdHash();
                try
                {
                    var response = await _server.ValidateAsync(stored.LicenseKey, machine, _appVersion, cancellationToken).ConfigureAwait(false);
                    var grant = AcceptResponse(response, machine);
                    var updated = stored.WithValidation(grant, response.Signature, _clock.UtcNow);
                    Persist(updated);
                    _logger.Info("Xác thực licence trực tuyến thành công (" + updated.KeyHint + ")");
                    return Publish(LicenseEvaluator.Evaluate(updated, machine, _clock.UtcNow)).Status;
                }
                catch (LicenseException ex) when (ex.Failure == LicenseFailure.ServerUnavailable)
                {
                    // Server lỗi không phải thu hồi: dùng thời gian ngoại tuyến còn lại (FR-LIC-005).
                    _logger.Warning("Không kết nối được licence server; dùng thời gian ngoại tuyến còn lại");
                    return Publish(EvaluateStored(true)).Status;
                }
                catch (LicenseException ex) when (ex.Failure == LicenseFailure.Revoked || ex.Failure == LicenseFailure.Expired)
                {
                    var marked = ex.Failure == LicenseFailure.Revoked ? stored.AsRevoked(_clock.UtcNow) : stored.WithSeen(_clock.UtcNow);
                    Persist(marked);
                    _logger.Warning("Licence bị từ chối bởi server: " + ex.Failure);
                    return Publish(LicenseEvaluator.Evaluate(marked, machine, _clock.UtcNow)).Status;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<LicenseStatus> ActivateAsync(string licenseKey, CancellationToken cancellationToken)
        {
            var key = (licenseKey ?? string.Empty).Trim();
            if (key.Length == 0) throw new LicenseException(LicenseFailure.KeyInvalid, "Chưa nhập licence key");

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var machine = _machine.GetMachineIdHash();
                var response = await _server.ActivateAsync(key, machine, _appVersion, cancellationToken).ConfigureAwait(false);
                var grant = AcceptResponse(response, machine);
                var now = _clock.UtcNow;
                var stored = new StoredLicense(key, grant, response.Signature, now, now, false);
                Persist(stored);
                _logger.Info("Kích hoạt licence thành công (" + stored.KeyHint + ")");
                return Publish(LicenseEvaluator.Evaluate(stored, machine, now)).Status;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<LicenseStatus> DeactivateAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var load = SafeLoad();
                if (load.Status == LicenseLoadStatus.Ok)
                {
                    // Phải giải phóng máy trên server trước; nếu không được thì giữ nguyên file để không mất quyền.
                    await _server.DeactivateAsync(load.License.LicenseKey, _machine.GetMachineIdHash(), cancellationToken).ConfigureAwait(false);
                    _logger.Info("Hủy kích hoạt licence (" + load.License.KeyHint + ")");
                }

                try
                {
                    _store.Delete();
                }
                catch (Exception ex)
                {
                    throw new LicenseException(LicenseFailure.StorageFailed, "Không xóa được license.dat: " + ex.Message);
                }

                return Publish(LicenseInfo.NotActivated).Status;
            }
            finally
            {
                _gate.Release();
            }
        }

        private LicenseGrant AcceptResponse(LicenseServerResponse response, string machine)
        {
            if (response == null || response.Grant == null || string.IsNullOrEmpty(response.Signature))
            {
                throw new LicenseException(LicenseFailure.ResponseRejected, "Phản hồi licence không hợp lệ");
            }

            if (!_verifier.Verify(response.Grant.ToSigningPayload(), response.Signature))
            {
                throw new LicenseException(LicenseFailure.ResponseRejected, "Chữ ký licence không hợp lệ");
            }

            if (!string.Equals(response.Grant.MachineIdHash, machine, StringComparison.Ordinal))
            {
                throw new LicenseException(LicenseFailure.KeyBoundToOtherMachine, "Licence không thuộc máy này");
            }

            if (response.Grant.ExpiresAtUtc.HasValue && response.Grant.ExpiresAtUtc.Value <= _clock.UtcNow)
            {
                throw new LicenseException(LicenseFailure.Expired, "Licence đã hết hạn");
            }

            return response.Grant;
        }

        private LicenseInfo EvaluateStored(bool recordSeen)
        {
            var load = SafeLoad();
            if (load.Status == LicenseLoadStatus.NotFound) return LicenseInfo.NotActivated;
            if (load.Status == LicenseLoadStatus.Corrupt)
            {
                return new LicenseInfo(LicenseStatus.Invalid, LicenseIssue.Corrupt, null, string.Empty, null, null, null);
            }

            var stored = load.License;
            if (!_verifier.Verify(stored.Grant.ToSigningPayload(), stored.Signature))
            {
                return new LicenseInfo(LicenseStatus.Invalid, LicenseIssue.SignatureInvalid, stored.Grant.LicenseType, stored.KeyHint, null, null, null);
            }

            var now = _clock.UtcNow;
            var info = LicenseEvaluator.Evaluate(stored, _machine.GetMachineIdHash(), now);
            if (recordSeen && info.Issue != LicenseIssue.ClockTampered && now > stored.LastSeenUtc)
            {
                TryPersist(stored.WithSeen(now));
            }

            return info;
        }

        private LicenseLoadResult SafeLoad()
        {
            try
            {
                return _store.Load();
            }
            catch (Exception ex)
            {
                _logger.Error("Không đọc được license.dat", ex);
                return new LicenseLoadResult(LicenseLoadStatus.Corrupt, null);
            }
        }

        private void Persist(StoredLicense license)
        {
            try
            {
                _store.Save(license);
            }
            catch (Exception ex)
            {
                _logger.Error("Không ghi được license.dat", ex);
                throw new LicenseException(LicenseFailure.StorageFailed, "Không lưu được licence: " + ex.Message);
            }
        }

        private void TryPersist(StoredLicense license)
        {
            try
            {
                _store.Save(license);
            }
            catch (Exception ex)
            {
                _logger.Warning("Không cập nhật được mốc thời gian licence: " + ex.GetType().Name);
            }
        }

        private LicenseInfo Publish(LicenseInfo info)
        {
            _info = info;
            var handler = InfoChanged;
            if (handler != null) handler(this, EventArgs.Empty);
            return info;
        }
    }
}
