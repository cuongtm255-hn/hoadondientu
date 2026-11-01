using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Companies;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class ShutdownCoordinatorTests
    {
        private List<string> _log;
        private Jobs _jobs;
        private Prompt _prompt;
        private Manager _manager;
        private Session _session;
        private ShutdownCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _jobs = new Jobs(_log);
            _prompt = new Prompt(_log);
            _session = new Session(_log);
            _manager = new Manager(_log, new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "A", @"C:\data\A", null));
            _coordinator = new ShutdownCoordinator(_manager, _jobs, _session, _prompt, new NullLogger());
        }

        private ShutdownResult Run()
        {
            return _coordinator.TryShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void NoJob_ClearsSessionThenContext_WithoutPrompt()
        {
            var result = Run();

            Assert.That(result.CanClose, Is.True);
            Assert.That(_log, Is.EqualTo(new[] { "session.clear", "context.close" }));
        }

        [Test]
        public void NoContext_StillClearsSession()
        {
            _manager.Current = null;

            var result = Run();

            Assert.That(result.CanClose, Is.True);
            Assert.That(_log, Is.EqualTo(new[] { "session.clear", "context.close" }));
        }

        [Test]
        public void JobRunning_StayOpen_ChangesNothing()
        {
            _jobs.Running = true;
            _prompt.Choice = ShutdownChoice.StayOpen;

            var result = Run();

            Assert.That(result.CanClose, Is.False);
            Assert.That(result.Message, Is.Null);
            Assert.That(_log, Is.EqualTo(new[] { "prompt" }));
        }

        [TestCase(ShutdownChoice.WaitForCompletion, "wait")]
        [TestCase(ShutdownChoice.PauseAndExit, "pause")]
        [TestCase(ShutdownChoice.CancelJobAndExit, "cancel")]
        public void JobRunning_ResolvesJobBeforeClosingSessionAndContext(ShutdownChoice choice, string expected)
        {
            _jobs.Running = true;
            _prompt.Choice = choice;

            var result = Run();

            Assert.That(result.CanClose, Is.True);
            Assert.That(_log, Is.EqualTo(new[] { "prompt", expected, "session.clear", "context.close" }));
        }

        [Test]
        public void PauseFails_DoesNotClose_KeepsSessionAndContext()
        {
            _jobs.Running = true;
            _jobs.PauseException = new InvalidOperationException("checkpoint failed");
            _prompt.Choice = ShutdownChoice.PauseAndExit;

            var result = Run();

            Assert.That(result.CanClose, Is.False);
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(_log, Does.Not.Contain("session.clear"));
            Assert.That(_log, Does.Not.Contain("context.close"));
        }

        [Test]
        public void CannotDetermineJobs_DoesNotClose()
        {
            _jobs.QueryException = new InvalidOperationException("db");

            var result = Run();

            Assert.That(result.CanClose, Is.False);
            Assert.That(_log, Is.Empty);
        }

        [Test]
        public void CleanupFailures_DoNotTrapTheApp()
        {
            _session.Throw = true;
            _manager.ThrowOnClose = true;

            var result = Run();

            Assert.That(result.CanClose, Is.True);
        }

        [Test]
        public void Cancellation_Propagates()
        {
            _jobs.QueryException = new OperationCanceledException();

            Assert.Throws<OperationCanceledException>(() => Run());
        }

        private sealed class Prompt : IShutdownPrompt
        {
            private readonly List<string> _log;

            public Prompt(List<string> log)
            {
                _log = log;
            }

            public ShutdownChoice Choice { get; set; }

            public ShutdownChoice Ask(string companyName)
            {
                _log.Add("prompt");
                return Choice;
            }
        }

        private sealed class Jobs : IActiveJobService
        {
            private readonly List<string> _log;

            public Jobs(List<string> log)
            {
                _log = log;
            }

            public bool Running { get; set; }
            public Exception QueryException { get; set; }
            public Exception PauseException { get; set; }

            public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                if (QueryException != null) throw QueryException;
                return Task.FromResult(Running);
            }

            public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                _log.Add("wait");
                return Task.FromResult(true);
            }

            public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                if (PauseException != null) throw PauseException;
                _log.Add("pause");
                return Task.FromResult(true);
            }

            public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                _log.Add("cancel");
                return Task.FromResult(true);
            }
        }

        private sealed class Manager : ICompanyContextManager
        {
            private readonly List<string> _log;

            public Manager(List<string> log, CompanyContext current)
            {
                _log = log;
                Current = current;
            }

            public bool ThrowOnClose { get; set; }
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
            public event EventHandler ContextChanged { add { } remove { } }

            public Task<CompanyContext> OpenAsync(CompanyProfile target, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public void CloseCurrent()
            {
                _log.Add("context.close");
                if (ThrowOnClose) throw new InvalidOperationException("dispose failed");
            }
        }

        private sealed class Session : IPortalSessionStore
        {
            private readonly List<string> _log;

            public Session(List<string> log)
            {
                _log = log;
            }

            public bool Throw { get; set; }
            public event EventHandler SessionChanged { add { } remove { } }
            public bool HasActiveSession { get { return false; } }
            public PortalSessionSnapshot GetSnapshot() { return null; }
            public PortalSession GetSessionForApi() { return null; }
            public void Set(PortalSession session) { }

            public void Clear()
            {
                _log.Add("session.clear");
                if (Throw) throw new InvalidOperationException("clear failed");
            }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
