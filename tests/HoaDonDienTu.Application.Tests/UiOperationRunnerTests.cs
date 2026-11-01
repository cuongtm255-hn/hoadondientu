using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class UiOperationRunnerTests
    {
        private List<UiOperationState> _states;
        private RecordingLogger _logger;
        private UiOperationRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _states = new List<UiOperationState>();
            _logger = new RecordingLogger();
            _runner = new UiOperationRunner(new UserErrorMapper(), _logger);
        }

        private void Run(string name, Func<CancellationToken, Task> op)
        {
            _runner.RunAsync(name, op, s => _states.Add(s)).GetAwaiter().GetResult();
        }

        [Test]
        public void Success_ReportsBusyThenIdle()
        {
            Run("op", t => Task.FromResult(true));

            Assert.That(_states.Count, Is.EqualTo(2));
            Assert.That(_states[0].IsBusy, Is.True);
            Assert.That(_states[1].IsBusy, Is.False);
            Assert.That(_states[1].Error, Is.Null);
            Assert.That(_states[1].WasCancelled, Is.False);
        }

        [Test]
        public void Failure_MapsError_DoesNotThrow_AndRestoresIdle()
        {
            Run("op", t => { throw new InvalidOperationException("boom password=hunter2"); });

            var last = _states[_states.Count - 1];
            Assert.That(last.IsBusy, Is.False);
            Assert.That(last.Error, Is.Not.Null);
            Assert.That(last.Error.Code, Is.EqualTo("UNEXPECTED"));
            Assert.That(last.Error.CorrelationId, Is.Not.Null.And.Not.Empty);
            Assert.That(_logger.Errors, Has.Count.EqualTo(1));
            Assert.That(_logger.Errors[0], Does.Contain(last.Error.CorrelationId));
        }

        [Test]
        public void Failure_UserMessage_DoesNotLeakExceptionText()
        {
            Run("op", t => { throw new InvalidOperationException("Authorization: Bearer topsecret"); });

            var error = _states[_states.Count - 1].Error;
            Assert.That(error.Message + error.SuggestedAction + error.Title, Does.Not.Contain("topsecret"));
        }

        [Test]
        public void Cancellation_ReportsCancelled_NotError()
        {
            Run("op", t => { throw new OperationCanceledException(); });

            var last = _states[_states.Count - 1];
            Assert.That(last.WasCancelled, Is.True);
            Assert.That(last.Error, Is.Null);
            Assert.That(_logger.Errors, Is.Empty);
        }

        [Test]
        public void ReEntry_SameName_Ignored_WhileRunning()
        {
            var release = new TaskCompletionSource<bool>();
            var calls = 0;
            var first = _runner.RunAsync("op", t => { calls++; return release.Task; }, s => _states.Add(s));
            _runner.RunAsync("op", t => { calls++; return Task.FromResult(true); }, s => _states.Add(s)).GetAwaiter().GetResult();

            release.SetResult(true);
            first.GetAwaiter().GetResult();

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void AfterCompletion_SameName_CanRunAgain()
        {
            var calls = 0;
            Run("op", t => { calls++; return Task.FromResult(true); });
            Run("op", t => { calls++; return Task.FromResult(true); });

            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void DifferentNames_RunIndependently()
        {
            var release = new TaskCompletionSource<bool>();
            var other = 0;
            var first = _runner.RunAsync("a", t => release.Task, s => { });
            _runner.RunAsync("b", t => { other++; return Task.FromResult(true); }, s => { }).GetAwaiter().GetResult();
            release.SetResult(true);
            first.GetAwaiter().GetResult();

            Assert.That(other, Is.EqualTo(1));
        }

        [Test]
        public void CancelAll_CancelsRunningOperationToken()
        {
            var started = new TaskCompletionSource<bool>();
            var task = _runner.RunAsync("op", async t =>
            {
                started.SetResult(true);
                await Task.Delay(Timeout.Infinite, t);
            }, s => _states.Add(s));

            started.Task.GetAwaiter().GetResult();
            _runner.CancelAll();
            task.GetAwaiter().GetResult();

            Assert.That(_states[_states.Count - 1].WasCancelled, Is.True);
        }

        [Test]
        public void RenderFailure_DoesNotBreakRunner()
        {
            Assert.DoesNotThrow(() =>
                _runner.RunAsync("op", t => Task.FromResult(true), s => { if (!s.IsBusy) throw new InvalidOperationException("ui"); })
                    .GetAwaiter().GetResult());
        }

        [Test]
        public void Mapper_CoversKnownExceptions()
        {
            var mapper = new UserErrorMapper();

            Assert.That(mapper.Map(new EntitlementDeniedException(AppFeature.NewDownload), "x", "c").Category, Is.EqualTo(ErrorCategory.Licence));
            Assert.That(mapper.Map(new WebException("net"), "x", "c").Category, Is.EqualTo(ErrorCategory.Network));
            Assert.That(mapper.Map(new IOException("io"), "x", "c").Category, Is.EqualTo(ErrorCategory.Storage));
            Assert.That(mapper.Map(new FormatException("f"), "x", "c").Category, Is.EqualTo(ErrorCategory.Validation));
            Assert.That(mapper.Map(new StorageGuardException("g"), "x", "c").Category, Is.EqualTo(ErrorCategory.Storage));
            Assert.That(mapper.Map(new CompanyStorageException(CompanySwitchBlocker.DatabaseInvalid, "d"), "x", "c").Category,
                Is.EqualTo(ErrorCategory.Database));
            Assert.That(mapper.Map(new OperationCanceledException(), "x", "c").Category, Is.EqualTo(ErrorCategory.Cancelled));

            var unavailable = mapper.Map(new FeatureNotAvailableException("Chưa có chức năng này."), "x", "c");
            Assert.That(unavailable.Code, Is.EqualTo("NOT_AVAILABLE"));
            Assert.That(unavailable.Message, Is.EqualTo("Chưa có chức năng này."));
        }

        private sealed class RecordingLogger : IApplicationLogger
        {
            public List<string> Errors = new List<string>();

            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { Errors.Add(message); }
        }
    }
}
