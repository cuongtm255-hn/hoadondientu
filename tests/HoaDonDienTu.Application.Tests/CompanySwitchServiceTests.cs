using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class CompanySwitchServiceTests
    {
        private Env _env;

        [SetUp]
        public void SetUp()
        {
            _env = new Env();
        }

        [Test]
        public void Switch_NoJob_ClosesOldBeforeOpeningNew_AndClearsSessionFirst()
        {
            _env.OpenInitial(_env.A);

            var result = _env.Switch(_env.B, ActiveJobResolution.NotRequired);

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.Switched));
            Assert.That(_env.Manager.Current.CompanyId, Is.EqualTo(_env.B.Id));
            Assert.That(_env.Log, Is.EqualTo(new[] { "session.clear", "close:A", "open:B" }));
        }

        [Test]
        public void Switch_JobRunning_NoResolution_Blocked_NothingChanged()
        {
            _env.OpenInitial(_env.A);
            _env.Jobs.Running = true;

            var assessment = _env.Service.AssessAsync(_env.B.Id, CancellationToken.None).GetAwaiter().GetResult();
            var result = _env.Switch(_env.B, ActiveJobResolution.NotRequired);

            Assert.That(assessment.RequiresJobResolution, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.Blocked));
            Assert.That(result.Blocker, Is.EqualTo(CompanySwitchBlocker.DownloadRunning));
            Assert.That(_env.Manager.Current.CompanyId, Is.EqualTo(_env.A.Id));
            Assert.That(_env.Log, Is.Empty);
        }

        [TestCase(ActiveJobResolution.PauseAndSwitch, "pause")]
        [TestCase(ActiveJobResolution.CancelAndSwitch, "cancel")]
        [TestCase(ActiveJobResolution.WaitForCompletion, "wait")]
        public void Switch_JobRunning_ResolvesJobFirst(ActiveJobResolution resolution, string expected)
        {
            _env.OpenInitial(_env.A);
            _env.Jobs.Running = true;

            var result = _env.Switch(_env.B, resolution);

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.Switched));
            Assert.That(_env.Log[0], Is.EqualTo(expected));
            Assert.That(_env.Log, Is.EqualTo(new[] { expected, "session.clear", "close:A", "open:B" }));
        }

        [Test]
        public void Switch_TargetOpenFails_RestoresPrevious()
        {
            _env.OpenInitial(_env.A);
            _env.Opener.Failing.Add(_env.B.Id);

            var result = _env.Switch(_env.B, ActiveJobResolution.NotRequired);

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.RestoredPrevious));
            Assert.That(result.Blocker, Is.EqualTo(CompanySwitchBlocker.DatabaseInvalid));
            Assert.That(_env.Manager.Current.CompanyId, Is.EqualTo(_env.A.Id));
            Assert.That(_env.Log, Is.EqualTo(new[] { "session.clear", "close:A", "open:B", "open:A" }));
        }

        [Test]
        public void Switch_TargetAndPreviousFail_RecoveryRequired()
        {
            _env.OpenInitial(_env.A);
            _env.Opener.Failing.Add(_env.B.Id);
            _env.Opener.Failing.Add(_env.A.Id);

            var result = _env.Switch(_env.B, ActiveJobResolution.NotRequired);

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.RecoveryRequired));
            Assert.That(_env.Manager.HasCurrent, Is.False);
        }

        [Test]
        public void Switch_ToCurrent_Unchanged_NoSideEffects()
        {
            _env.OpenInitial(_env.A);

            var result = _env.Switch(_env.A, ActiveJobResolution.NotRequired);

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.Unchanged));
            Assert.That(_env.Log, Is.Empty);
        }

        [Test]
        public void Switch_UnknownTarget_Blocked()
        {
            _env.OpenInitial(_env.A);

            var result = _env.Service.SwitchAsync(CompanyId.NewId(), ActiveJobResolution.NotRequired, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.Blocked));
            Assert.That(_env.Manager.Current.CompanyId, Is.EqualTo(_env.A.Id));
        }

        [Test]
        public void Switch_StandardMode_BetweenCompanies_Blocked()
        {
            _env.OpenInitial(_env.A);
            _env.Entitlement.Mode = AppMode.Standard;

            var result = _env.Switch(_env.B, ActiveJobResolution.NotRequired);

            Assert.That(result.Blocker, Is.EqualTo(CompanySwitchBlocker.LicenceReadOnly));
            Assert.That(_env.Manager.Current.CompanyId, Is.EqualTo(_env.A.Id));
        }

        [Test]
        public void Switch_ReadOnlyService_BetweenExistingCompanies_Allowed()
        {
            _env.OpenInitial(_env.A);
            _env.Entitlement.Mode = AppMode.ReadOnlyService;

            var result = _env.Switch(_env.B, ActiveJobResolution.NotRequired);

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.Switched));
        }

        [Test]
        public void Switch_InitialOpen_AllowedInStandard()
        {
            _env.Entitlement.Mode = AppMode.Standard;

            var result = _env.Switch(_env.A, ActiveJobResolution.NotRequired);

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.Switched));
        }

        [Test]
        public void Switch_WhileAnotherSwitchRuns_Blocked()
        {
            _env.OpenInitial(_env.A);
            var release = new TaskCompletionSource<bool>();
            _env.Jobs.Running = true;
            _env.Jobs.WaitTask = release.Task;

            var first = _env.Service.SwitchAsync(_env.B.Id, ActiveJobResolution.WaitForCompletion, CancellationToken.None);
            var second = _env.Switch(_env.B, ActiveJobResolution.NotRequired);
            release.SetResult(true);
            first.GetAwaiter().GetResult();

            Assert.That(second.Blocker, Is.EqualTo(CompanySwitchBlocker.SwitchInProgress));
        }

        private sealed class Env
        {
            public Env()
            {
                A = new CompanyProfile(CompanyId.NewId(), "A", TaxCode.Parse("0101234561"));
                B = new CompanyProfile(CompanyId.NewId(), "B", TaxCode.Parse("0101234562"));
                Log = new List<string>();
                Opener = new FakeOpener(Log);
                Manager = new LoggingManager(Opener, Log);
                Jobs = new FakeJobs(Log);
                Session = new FakeSession(Log);
                Entitlement = new FakeEntitlement { Mode = AppMode.Service };
                Service = new CompanySwitchService(
                    new FakeCatalog(new[] { A, B }), Manager, Jobs, Session, Entitlement, new NullLogger());
            }

            public CompanyProfile A { get; private set; }
            public CompanyProfile B { get; private set; }
            public List<string> Log { get; private set; }
            public FakeOpener Opener { get; private set; }
            public LoggingManager Manager { get; private set; }
            public FakeJobs Jobs { get; private set; }
            public FakeSession Session { get; private set; }
            public FakeEntitlement Entitlement { get; private set; }
            public CompanySwitchService Service { get; private set; }

            public void OpenInitial(CompanyProfile company)
            {
                Manager.OpenAsync(company, CancellationToken.None).GetAwaiter().GetResult();
                Log.Clear();
            }

            public CompanySwitchResult Switch(CompanyProfile target, ActiveJobResolution resolution)
            {
                return Service.SwitchAsync(target.Id, resolution, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        private sealed class FakeCatalog : ICompanyCatalog
        {
            private readonly CatalogSnapshot _snapshot;

            public FakeCatalog(IList<CompanyProfile> companies)
            {
                _snapshot = new CatalogSnapshot(CatalogLoadStatus.Loaded, companies, true);
            }

            public Task<CatalogSnapshot> LoadAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_snapshot);
            }
        }

        private sealed class FakeOpener : ICompanyStorageOpener
        {
            private readonly List<string> _log;

            public FakeOpener(List<string> log)
            {
                _log = log;
                Failing = new HashSet<CompanyId>();
            }

            public HashSet<CompanyId> Failing { get; private set; }

            public Task<CompanyContext> OpenAsync(CompanyProfile company, CancellationToken cancellationToken)
            {
                _log.Add("open:" + company.DisplayName);
                if (Failing.Contains(company.Id))
                {
                    throw new CompanyStorageException(CompanySwitchBlocker.DatabaseInvalid, "db hỏng");
                }

                return Task.FromResult(new CompanyContext(company.Id, company.TaxCode, company.DisplayName, @"C:\data\" + company.DisplayName, null));
            }
        }

        /// <summary>Manager thật, chỉ thêm ghi nhật ký thứ tự đóng.</summary>
        private sealed class LoggingManager : ICompanyContextManager
        {
            private readonly CompanyContextManager _inner;
            private readonly List<string> _log;

            public LoggingManager(ICompanyStorageOpener opener, List<string> log)
            {
                _inner = new CompanyContextManager(opener);
                _log = log;
            }

            public event EventHandler ContextChanged
            {
                add { _inner.ContextChanged += value; }
                remove { _inner.ContextChanged -= value; }
            }

            public bool HasCurrent { get { return _inner.HasCurrent; } }
            public CompanyContext Current { get { return _inner.Current; } }

            public Task<CompanyContext> OpenAsync(CompanyProfile target, CancellationToken cancellationToken)
            {
                return _inner.OpenAsync(target, cancellationToken);
            }

            public void CloseCurrent()
            {
                if (_inner.Current != null) _log.Add("close:" + _inner.Current.DisplayName);
                _inner.CloseCurrent();
            }
        }

        private sealed class FakeJobs : IActiveJobService
        {
            private readonly List<string> _log;

            public FakeJobs(List<string> log)
            {
                _log = log;
            }

            public bool Running { get; set; }
            public Task WaitTask { get; set; }

            public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                return Task.FromResult(Running);
            }

            public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                _log.Add("wait");
                return WaitTask ?? Task.FromResult(true);
            }

            public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                _log.Add("pause");
                return Task.FromResult(true);
            }

            public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                _log.Add("cancel");
                return Task.FromResult(true);
            }
        }

        private sealed class FakeSession : IPortalSessionStore
        {
            private readonly List<string> _log;

            public FakeSession(List<string> log)
            {
                _log = log;
            }

            public bool IsAuthenticated { get; set; }
            public TaxCode TaxCode { get; set; }

            public void Clear()
            {
                _log.Add("session.clear");
                IsAuthenticated = false;
                TaxCode = null;
            }
        }

        private sealed class FakeEntitlement : IEntitlementService
        {
            public AppMode Mode { get; set; }

            public AppMode CurrentMode { get { return Mode; } }

            public bool IsAllowed(AppFeature feature)
            {
                return Mode == AppMode.Service || (Mode == AppMode.Standard && feature != AppFeature.MultiCompany);
            }

            public void Demand(AppFeature feature)
            {
                if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature);
            }

            public event EventHandler ModeChanged { add { } remove { } }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
