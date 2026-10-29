using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DawnOfThePhoenix.Web.Options;
using DawnOfThePhoenix.Web.Pages.Api;
using DawnOfThePhoenix.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class PaidBookingServiceShould
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "phoenix-alert-checks-" + Guid.NewGuid());
        var cal = new CalComOptions { Username = "dawn", WebhookSecret = "fixture-webhook-secret", ThirtyMinuteEventId = 30, SixtyMinuteEventId = 60 };
        var alerts = new BookingAlertOptions
        {
            TwilioAccountSid = "fixture-sid", TwilioAuthToken = "fixture-token", SmsFrom = "+15005550006",
            SmsTo = "+15005550007", StateDirectory = directory
        };
        var handler = new AlertHandler();
        var emailSettings = Options.Create(new PaymentEmailOptions { Enabled = true, Password = "fixture-smtp-password" });
        PaidBookingService Service() => new(new HttpClient(handler), Options.Create(cal), Options.Create(alerts), new BookingEmailSender(handler), emailSettings);
        var service = Service();
        static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
        byte[] Body(string uid = "booking-1", string trigger = "BOOKING_PAID", int eventId = 30, string organizer = "dawn") =>
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                triggerEvent = trigger,
                payload = new { uid, eventTypeId = eventId, startTime = "2026-11-01T10:00:00-05:00",
                    organizer = new { username = organizer }, attendees = new[] { new { name = "Test Client" } },
                    additionalNotes = "private-reading-question" }
            });
        string Sign(byte[] body) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(cal.WebhookSecret), body));
        async Task<int> Post(byte[] body, string? signature, long? length = null, PaidBookingService? target = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(body);
            context.Request.ContentLength = length;
            if (signature != null) context.Request.Headers["x-cal-signature-256"] = signature;
            var page = new CalWebhookModel(target ?? service, NullLogger<CalWebhookModel>.Instance)
            { PageContext = new PageContext { HttpContext = context } };
            return ((StatusCodeResult)await page.OnPostAsync(default)).StatusCode;
        }
        try
        {
            var body = Body();
            Check(await Post(body, null) == 401, "CalWebhookShould.Reject_On_Post_WhenSignatureMissing");
            Check(await Post(body, new string('z', 64)) == 401, "CalWebhookShould.Reject_On_Post_WhenSignatureMalformed");
            Check(await Post(body, Sign(Body("tampered"))) == 401, "CalWebhookShould.Reject_On_Post_WhenBodyTampered");
            Check(await Post(body, Sign(body), 65537) == 413, "CalWebhookShould.Reject_On_Post_WhenDeclaredBodyTooLarge");
            Check(await Post(new byte[65537], null) == 413, "CalWebhookShould.Reject_On_Post_WhenChunkedBodyTooLarge");
            foreach (var invalid in new[] { "null", "{\"triggerEvent\":\"BOOKING_PAID\"}", "{\"triggerEvent\":\"BOOKING_PAID\",\"payload\":{\"eventTypeId\":30,\"organizer\":{\"username\":\"dawn\"}}}" })
            {
                var invalidBytes = Encoding.UTF8.GetBytes(invalid);
                Check(await Post(invalidBytes, Sign(invalidBytes)) == 400, "CalWebhookShould.Reject_On_Post_WhenPaidPayloadIncomplete");
            }
            var malformed = Encoding.UTF8.GetBytes("{broken");
            Check(await Post(malformed, Sign(malformed)) == 400, "CalWebhookShould.Reject_On_Post_WhenSignedJsonInvalid");
            foreach (var trigger in new[] { "BOOKING_CREATED", "BOOKING_PAYMENT_INITIATED", "BOOKING_CANCELLED" })
            {
                var unpaid = Body(trigger: trigger);
                Check(await Post(unpaid, Sign(unpaid)) == 204, "CalWebhookShould.Ignore_On_Post_WhenEventIsNotPaid");
            }
            foreach (var unrelated in new[] { Body(eventId: 99), Body(organizer: "someone-else") })
                Check(await Post(unrelated, Sign(unrelated)) == 204, "CalWebhookShould.Ignore_On_Post_WhenBookingIsUnrelated");
            Check(handler.Requests.Count == 0 && !Directory.Exists(directory), "CalWebhookShould.PreserveState_On_Post_WhenRejectedOrIgnored");
            Check(await Post(body, Sign(body)) == 204 && handler.Requests.Count == 2, "CalWebhookShould.NotifyBothChannels_On_Post_WhenBookingPaid");
            Check(handler.Requests[0].Url == "smtp"
                && handler.Requests[0].Body.Contains("Paid session booked")
                && handler.Requests[0].Body.Contains("Test Client"), "PaidBookingServiceShould.SendToConfiguredEmail_On_HandleAsync");
            var sms = handler.Requests[1];
            Check(sms.Url.Contains("/Accounts/fixture-sid/Messages.json") && sms.Authorization.StartsWith("Basic ")
                && sms.Body.Contains("To=%2B15005550007") && sms.Body.Contains("15%3A00+UTC"), "PaidBookingServiceShould.SendToConfiguredPhoneWithUtcTime_On_HandleAsync");
            Check(!handler.Requests.Any(r => r.Body.Contains("private-reading-question")), "PaidBookingServiceShould.OmitReadingNotes_On_HandleAsync");
            Check(await Post(body, Sign(body), target: Service()) == 204 && handler.Requests.Count == 2,
                "PaidBookingServiceShould.SuppressBothChannels_On_HandleAsync_AfterRestart");
            Check(Directory.GetFiles(directory).All(p => !Path.GetFileName(p).Contains("booking-1")), "PaidBookingServiceShould.HashBookingReference_On_HandleAsync");
            handler.FailSms = true;
            var retry = Body("booking-retry", eventId: 60);
            Check(await Post(retry, Sign(retry)) == 503 && handler.Requests.Count == 4, "CalWebhookShould.RequestRetry_On_Post_WhenSmsFails");
            handler.FailSms = false;
            Check(await Post(retry, Sign(retry), target: Service()) == 204 && handler.Requests.Count == 5
                && handler.Requests[^1].Url.Contains("twilio.com"), "PaidBookingServiceShould.RetryOnlyFailedChannel_On_HandleAsync");
            handler.FailEmail = true;
            var emailRetry = Body("email-retry");
            Check(await Post(emailRetry, Sign(emailRetry)) == 503 && handler.Requests.Count == 7,
                "PaidBookingServiceShould.AttemptSms_On_HandleAsync_WhenEmailFails");
            handler.FailEmail = false;
            Check(await Post(emailRetry, Sign(emailRetry), target: Service()) == 204 && handler.Requests.Count == 8
                && handler.Requests[^1].Url == "smtp", "PaidBookingServiceShould.RetryOnlyEmail_On_HandleAsync_WhenSmsAlreadyAccepted");
            var locked = Body("locked");
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("locked")));
            using (var gate = new FileStream(Path.Combine(directory, key + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                Check(await Post(locked, Sign(locked)) == 503 && handler.Requests.Count == 8,
                    "CalWebhookShould.RequestRetry_On_Post_WhenAnotherDeliveryHoldsLock");
            alerts.SmsTo = "";
            Check(await Post(body, Sign(body)) == 503 && handler.Requests.Count == 8, "CalWebhookShould.FailClosed_On_Post_WhenConfigurationIncomplete");
            cal.WebhookSecret = "";
            Check(!service.Verify(body, Sign(body)), "PaidBookingServiceShould.Reject_On_Verify_WhenSecretMissing");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        Console.WriteLine("PASS: signed paid-booking webhooks, scoped events, two-channel alerts, durable deduplication, partial retries, and bounded requests.");
    }

    private sealed class BookingEmailSender(AlertHandler handler) : IPaymentEmailSender
    {
        public Task SendAsync(PaymentEmail email, CancellationToken cancellationToken)
        {
            handler.Requests.Add(("smtp", "", email.Subject + " " + email.Body));
            if (handler.FailEmail) throw new System.Net.Mail.SmtpException("Fixture failure");
            return Task.CompletedTask;
        }
    }

    private sealed class AlertHandler : HttpMessageHandler
    {
        public List<(string Url, string Authorization, string Body)> Requests { get; } = [];
        public bool FailSms { get; set; }
        public bool FailEmail { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.AbsoluteUri, request.Headers.Authorization!.ToString(),
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            var failed = request.RequestUri.Host == "api.twilio.com" ? FailSms : FailEmail;
            return new HttpResponseMessage(failed ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Accepted);
        }
    }
}
