using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    /// <summary>Trạng thái licence lưu trên máy (đã mã hóa bởi hạ tầng): mốc chạy đầu tiên, mốc thời gian lớn nhất từng thấy và mã kích hoạt.</summary>
    public sealed class LicenseRecord
    {
        public DateTime FirstRunUtc { get; set; }

        /// <summary>Mốc thời gian lớn nhất từng ghi nhận; đồng hồ bị chỉnh lùi không kéo dài được thời gian dùng thử.</summary>
        public DateTime LastSeenUtc { get; set; }

        public string ActivationKey { get; set; }
        public DateTime? ActivatedAtUtc { get; set; }
    }

    /// <summary>Nơi lưu <see cref="LicenseRecord"/>. Cài đặt thật ghi đồng thời file và registry để một nơi bị xóa thì nơi kia còn.</summary>
    public interface ILicenseStateStore
    {
        /// <summary>null khi chưa có trạng thái nào (lần chạy đầu tiên).</summary>
        LicenseRecord Load();

        void Save(LicenseRecord record);
    }

    /// <summary>Mã nhận diện máy dùng để cấp mã kích hoạt; người dùng gửi cho đơn vị phát hành.</summary>
    public interface IMachineCodeProvider
    {
        /// <summary>Dạng hiển thị, ví dụ <c>1A2B3C4D-5E6F7A8B-9C0D1E2F-3A4B5C6D</c>.</summary>
        string GetMachineCode();
    }

    public sealed class ActivationKeyCheck
    {
        public bool IsValid { get; set; }
        public LicenseFailure Failure { get; set; }
        public string Message { get; set; }
        public DateTime? IssuedAtUtc { get; set; }

        public static ActivationKeyCheck Ok(DateTime? issuedAtUtc)
        {
            return new ActivationKeyCheck { IsValid = true, Message = string.Empty, IssuedAtUtc = issuedAtUtc };
        }

        public static ActivationKeyCheck Fail(LicenseFailure failure, string message)
        {
            return new ActivationKeyCheck { IsValid = false, Failure = failure, Message = message };
        }
    }

    /// <summary>Kiểm tra mã kích hoạt: định dạng, chữ ký số của đơn vị phát hành, sản phẩm và mã máy.</summary>
    public interface IActivationKeyVerifier
    {
        ActivationKeyCheck Verify(string activationKey, string machineCode);
    }

    /// <summary>Kết quả đánh giá licence tại một thời điểm.</summary>
    public sealed class LicenseSnapshot
    {
        public string MachineCode { get; set; }
        public LicenseStatus Status { get; set; }

        /// <summary>Số ngày dùng thử còn lại (0 khi đã hết hoặc đã kích hoạt).</summary>
        public int RemainingTrialDays { get; set; }

        public DateTime FirstRunUtc { get; set; }
        public DateTime TrialEndsUtc { get; set; }
        public DateTime? ActivatedAtUtc { get; set; }
        public string KeyHint { get; set; }

        /// <summary>Có mã kích hoạt lưu trên máy nhưng không còn hợp lệ cho máy này (ví dụ đổi phần cứng).</summary>
        public bool StoredKeyRejected { get; set; }

        public string StoredKeyProblem { get; set; }

        public bool IsActivated
        {
            get { return Status == LicenseStatus.Valid; }
        }

        public bool IsTrialExpired
        {
            get { return Status == LicenseStatus.Expired; }
        }

        /// <summary>Được vào ứng dụng: đã kích hoạt hoặc còn trong thời gian dùng thử.</summary>
        public bool CanUse
        {
            get { return Status == LicenseStatus.Valid || Status == LicenseStatus.Trial; }
        }
    }

    /// <summary>
    /// Licence đơn giản theo máy (thay cơ chế máy chủ licence): dùng thử 30 ngày kể từ lần chạy đầu tiên, sau đó phải nhập mã kích hoạt.
    /// Mã kích hoạt là chuỗi do đơn vị phát hành ký số offline cho đúng mã máy này; ứng dụng chỉ giữ khóa công khai nên không tự sinh được mã.
    /// Không có kết nối mạng, không có hạn dùng sau khi kích hoạt.
    /// </summary>
    public sealed class MachineLicenseService : ILicenseService
    {
        public const int TrialDays = 30;

        private static readonly TimeSpan SaveSeenEvery = TimeSpan.FromHours(1);

        private readonly ILicenseStateStore _store;
        private readonly IMachineCodeProvider _machine;
        private readonly IActivationKeyVerifier _verifier;
        private readonly IClock _clock;
        private readonly IApplicationLogger _logger;
        private readonly object _gate = new object();
        private LicenseInfo _info = LicenseInfo.NotActivated;

        public MachineLicenseService(ILicenseStateStore store, IMachineCodeProvider machine, IActivationKeyVerifier verifier, IClock clock, IApplicationLogger logger)
        {
            if (store == null) throw new ArgumentNullException("store");
            if (machine == null) throw new ArgumentNullException("machine");
            if (verifier == null) throw new ArgumentNullException("verifier");
            if (clock == null) throw new ArgumentNullException("clock");
            if (logger == null) throw new ArgumentNullException("logger");
            _store = store;
            _machine = machine;
            _verifier = verifier;
            _clock = clock;
            _logger = logger;
        }

        public event EventHandler InfoChanged;

        public LicenseInfo CurrentInfo
        {
            get { lock (_gate) { return _info; } }
        }

        public string MachineCode
        {
            get { return _machine.GetMachineCode(); }
        }

        /// <summary>Đánh giá ngay (đọc trạng thái, tính thời gian dùng thử, kiểm mã kích hoạt đã lưu). Lần chạy đầu tiên bắt đầu thời gian dùng thử.</summary>
        public LicenseSnapshot Evaluate()
        {
            lock (_gate)
            {
                var snapshot = EvaluateCore();
                SetInfo(snapshot);
                return snapshot;
            }
        }

        public Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Evaluate().Status);
        }

        /// <summary>Không có máy chủ: kiểm tra trực tuyến giống kiểm tra cục bộ (giữ để khớp hợp đồng <see cref="ILicenseService"/>).</summary>
        public Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken)
        {
            return ValidateLocalAsync(cancellationToken);
        }

        /// <exception cref="LicenseException">Mã không đúng định dạng, sai chữ ký, sai sản phẩm hoặc thuộc máy khác.</exception>
        public Task<LicenseStatus> ActivateAsync(string licenseKey, CancellationToken cancellationToken)
        {
            var key = (licenseKey ?? string.Empty).Trim();
            if (key.Length == 0) throw new LicenseException(LicenseFailure.KeyInvalid, "Vui lòng nhập mã kích hoạt.");

            lock (_gate)
            {
                var check = _verifier.Verify(key, _machine.GetMachineCode());
                if (!check.IsValid)
                {
                    _logger.Warning("Mã kích hoạt bị từ chối: " + check.Failure);
                    throw new LicenseException(check.Failure, check.Message);
                }

                var record = LoadOrCreate();
                var now = EffectiveNow(record);
                record.ActivationKey = key;
                record.ActivatedAtUtc = check.IssuedAtUtc ?? now;
                Save(record);
                _logger.Info("Kích hoạt bản quyền thành công (" + LicenseKeys.Hint(key) + ")");

                var snapshot = EvaluateCore();
                SetInfo(snapshot);
                return Task.FromResult(snapshot.Status);
            }
        }

        /// <summary>Gỡ mã kích hoạt khỏi máy này (thời gian dùng thử không được làm mới).</summary>
        public Task<LicenseStatus> DeactivateAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var record = LoadOrCreate();
                record.ActivationKey = null;
                record.ActivatedAtUtc = null;
                Save(record);
                _logger.Info("Đã gỡ mã kích hoạt khỏi máy này");
                var snapshot = EvaluateCore();
                SetInfo(snapshot);
                return Task.FromResult(snapshot.Status);
            }
        }

        // ---- Nội bộ ----

        private LicenseSnapshot EvaluateCore()
        {
            var record = LoadOrCreate();
            var now = EffectiveNow(record);
            var machineCode = _machine.GetMachineCode();

            // Ghi lại mốc đã thấy (đồng hồ chỉ được tiến) nhưng không ghi đĩa mỗi lần đánh giá.
            if (now - record.LastSeenUtc >= SaveSeenEvery)
            {
                record.LastSeenUtc = now;
                Save(record);
            }

            var activated = false;
            var rejected = false;
            string rejectedProblem = null;
            if (!string.IsNullOrWhiteSpace(record.ActivationKey))
            {
                var check = _verifier.Verify(record.ActivationKey, machineCode);
                activated = check.IsValid;
                rejected = !check.IsValid;
                rejectedProblem = check.IsValid ? null : check.Message;
            }

            var elapsed = (int)(now.Date - record.FirstRunUtc.Date).TotalDays;
            var remaining = Math.Max(0, TrialDays - Math.Max(0, elapsed));
            return new LicenseSnapshot
            {
                MachineCode = machineCode,
                Status = activated ? LicenseStatus.Valid : remaining > 0 ? LicenseStatus.Trial : LicenseStatus.Expired,
                RemainingTrialDays = activated ? 0 : remaining,
                FirstRunUtc = record.FirstRunUtc,
                TrialEndsUtc = record.FirstRunUtc.Date.AddDays(TrialDays),
                ActivatedAtUtc = activated ? record.ActivatedAtUtc : null,
                KeyHint = activated ? LicenseKeys.Hint(record.ActivationKey) : string.Empty,
                StoredKeyRejected = rejected,
                StoredKeyProblem = rejectedProblem
            };
        }

        private LicenseRecord LoadOrCreate()
        {
            var record = _store.Load();
            var now = _clock.UtcNow;
            if (record == null)
            {
                record = new LicenseRecord { FirstRunUtc = now, LastSeenUtc = now };
                Save(record);
                return record;
            }

            // Mốc chạy đầu ở tương lai (đồng hồ bị chỉnh lúc đó) không được thành lợi thế: coi như bắt đầu từ bây giờ.
            if (record.FirstRunUtc > now && record.FirstRunUtc > record.LastSeenUtc) record.FirstRunUtc = now;
            if (record.LastSeenUtc < record.FirstRunUtc) record.LastSeenUtc = record.FirstRunUtc;
            return record;
        }

        private DateTime EffectiveNow(LicenseRecord record)
        {
            var now = _clock.UtcNow;
            return now >= record.LastSeenUtc ? now : record.LastSeenUtc;
        }

        private void Save(LicenseRecord record)
        {
            try
            {
                _store.Save(record);
            }
            catch (Exception ex)
            {
                // Không lưu được không được làm hỏng việc dùng ứng dụng; lần đánh giá sau thử lại.
                _logger.Warning("Không lưu được trạng thái bản quyền: " + ex.GetType().Name);
            }
        }

        private void SetInfo(LicenseSnapshot snapshot)
        {
            var issue = snapshot.StoredKeyRejected ? LicenseIssue.MachineMismatch : snapshot.IsTrialExpired ? LicenseIssue.Expired : LicenseIssue.None;
            var info = new LicenseInfo(
                snapshot.Status,
                snapshot.IsActivated ? LicenseIssue.None : issue,
                snapshot.IsActivated ? "PerMachine" : "Trial",
                snapshot.KeyHint,
                snapshot.IsActivated ? (DateTime?)null : snapshot.TrialEndsUtc,
                null,
                null);

            var changed = _info.Status != info.Status || _info.Issue != info.Issue || _info.ExpiresAtUtc != info.ExpiresAtUtc || _info.KeyHint != info.KeyHint;
            _info = info;
            if (!changed) return;
            var handler = InfoChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>Số ngày còn lại viết cho người dùng: "còn 12 ngày", "còn hôm nay".</summary>
        public static string DescribeRemaining(int days)
        {
            return days <= 0 ? "đã hết" : "còn " + days.ToString(CultureInfo.InvariantCulture) + " ngày";
        }
    }
}
