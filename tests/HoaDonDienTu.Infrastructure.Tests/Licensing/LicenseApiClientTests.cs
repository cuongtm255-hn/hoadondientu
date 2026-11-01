using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Licensing;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Licensing
{
    [TestFixture]
    public class LicenseApiClientTests
    {
        private StubHandler _handler;
        private LicenseApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _handler = new StubHandler();
            _client = new LicenseApiClient(new LicenseApiConfig("https://license.example.test/api/", TimeSpan.FromSeconds(5)), _handler);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
        }

        private const string GrantJson =
            "{\"licenseType\":\"Service\",\"machineIdHash\":\"m1\",\"issuedAtUtc\":\"2026-11-01T08:00:00Z\"," +
            "\"expiresAtUtc\":\"2027-11-01T08:00:00Z\",\"features\":[\"MultiCompany\",\"PortalLogin\",\"FutureThing\"],\"signature\":\"c2ln\"}";

        [Test]
        public async Task Activate_SendsOnlyKeyMachineAndVersion_ToTheRightUrl()
        {
            _handler.Reply(HttpStatusCode.OK, GrantJson);

            await _client.ActivateAsync("KEY-1", "m1", "1.2.3", CancellationToken.None);

            Assert.That(_handler.LastUrl, Is.EqualTo("https://license.example.test/api/activate"));
            var body = JObject.Parse(_handler.LastBody);
            Assert.That(body.Count, Is.EqualTo(3));
            Assert.That((string)body["licenseKey"], Is.EqualTo("KEY-1"));
            Assert.That((string)body["machineIdHash"], Is.EqualTo("m1"));
            Assert.That((string)body["appVersion"], Is.EqualTo("1.2.3"));
        }

        [Test]
        public async Task Activate_ParsesGrant_IgnoringUnknownFeatures()
        {
            _handler.Reply(HttpStatusCode.OK, GrantJson);

            var response = await _client.ActivateAsync("KEY-1", "m1", "1.0.0", CancellationToken.None);

            Assert.That(response.Grant.LicenseType, Is.EqualTo("Service"));
            Assert.That(response.Grant.MachineIdHash, Is.EqualTo("m1"));
            Assert.That(response.Grant.ExpiresAtUtc, Is.EqualTo(new DateTime(2027, 11, 1, 8, 0, 0, DateTimeKind.Utc)));
            Assert.That(response.Grant.Features, Is.EqualTo(new[] { AppFeature.MultiCompany, AppFeature.PortalLogin }));
            Assert.That(response.Signature, Is.EqualTo("c2ln"));
        }

        [Test]
        public async Task Validate_NullExpiry_MeansNoExpiry()
        {
            _handler.Reply(HttpStatusCode.OK, GrantJson.Replace("\"2027-11-01T08:00:00Z\"", "null"));

            var response = await _client.ValidateAsync("KEY-1", "m1", "1.0.0", CancellationToken.None);

            Assert.That(response.Grant.ExpiresAtUtc, Is.Null);
            Assert.That(_handler.LastUrl, Does.EndWith("/validate"));
        }

        [Test]
        public async Task Deactivate_SendsOnlyKeyAndMachine()
        {
            _handler.Reply(HttpStatusCode.OK, "{}");

            await _client.DeactivateAsync("KEY-1", "m1", CancellationToken.None);

            Assert.That(_handler.LastUrl, Does.EndWith("/deactivate"));
            Assert.That(JObject.Parse(_handler.LastBody).Count, Is.EqualTo(2));
        }

        [TestCase("key_invalid", LicenseFailure.KeyInvalid)]
        [TestCase("bound_to_other_machine", LicenseFailure.KeyBoundToOtherMachine)]
        [TestCase("expired", LicenseFailure.Expired)]
        [TestCase("revoked", LicenseFailure.Revoked)]
        [TestCase("something_new", LicenseFailure.KeyInvalid)]
        public void Rejection_IsMappedByErrorCode(string code, LicenseFailure expected)
        {
            _handler.Reply(HttpStatusCode.Forbidden, "{\"error\":\"" + code + "\"}");

            var ex = Assert.ThrowsAsync<LicenseException>(() => _client.ActivateAsync("KEY-1", "m1", "1.0.0", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(expected));
        }

        [Test]
        public void Rejection_WithUnreadableBody_IsKeyInvalid()
        {
            _handler.Reply(HttpStatusCode.BadRequest, "<html>nope</html>");

            var ex = Assert.ThrowsAsync<LicenseException>(() => _client.ActivateAsync("KEY-1", "m1", "1.0.0", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.KeyInvalid));
        }

        [TestCase(HttpStatusCode.InternalServerError)]
        [TestCase(HttpStatusCode.BadGateway)]
        [TestCase(HttpStatusCode.ServiceUnavailable)]
        [TestCase((HttpStatusCode)429)]
        public void ServerErrors_AreUnavailable_NotRevocation(HttpStatusCode status)
        {
            _handler.Reply(status, "{\"error\":\"revoked\"}");

            var ex = Assert.ThrowsAsync<LicenseException>(() => _client.ValidateAsync("KEY-1", "m1", "1.0.0", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.ServerUnavailable));
        }

        [Test]
        public void NetworkFailure_IsUnavailable()
        {
            _handler.Throw = new HttpRequestException("dns");

            var ex = Assert.ThrowsAsync<LicenseException>(() => _client.ValidateAsync("KEY-1", "m1", "1.0.0", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.ServerUnavailable));
        }

        [Test]
        public void Timeout_IsUnavailable()
        {
            _handler.Throw = new TaskCanceledException();

            var ex = Assert.ThrowsAsync<LicenseException>(() => _client.ValidateAsync("KEY-1", "m1", "1.0.0", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.ServerUnavailable));
        }

        [Test]
        public void UserCancellation_IsNotReportedAsServerDown()
        {
            _handler.Throw = new TaskCanceledException();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(() => _client.ValidateAsync("KEY-1", "m1", "1.0.0", cts.Token));
        }

        [TestCase("not json")]
        [TestCase("{\"licenseType\":\"Service\",\"issuedAtUtc\":\"not a date\"}")]
        public void MalformedGrant_IsResponseRejected(string body)
        {
            _handler.Reply(HttpStatusCode.OK, body);

            var ex = Assert.ThrowsAsync<LicenseException>(() => _client.ActivateAsync("KEY-1", "m1", "1.0.0", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.ResponseRejected));
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private HttpStatusCode _status = HttpStatusCode.OK;
            private string _body = string.Empty;

            public Exception Throw { get; set; }
            public string LastUrl { get; private set; }
            public string LastBody { get; private set; }

            public void Reply(HttpStatusCode status, string body)
            {
                _status = status;
                _body = body;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastUrl = request.RequestUri.ToString();
                LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync();
                if (Throw != null) throw Throw;
                cancellationToken.ThrowIfCancellationRequested();
                return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
            }
        }
    }
}
