using System.Text;
using DawnOfThePhoenix.Web.Services;
using DawnOfThePhoenix.Web.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

internal static class PaymentWebhookShould
{
    public static async Task RunAsync()
    {
        static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
        var signatureNow = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var webhookBody = "{\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_test_12345678901\",\"payment_status\":\"paid\",\"metadata\":{\"service\":\"questions\"},\"amount_total\":800,\"currency\":\"usd\",\"customer_details\":{\"email\":\"reply@example.com\"},\"custom_fields\":[{\"key\":\"question1\",\"text\":{\"value\":\"A question?\"}}]}}}";
        const string signingSecret = "whsec_fixture_only";
        string Sign(string body, DateTimeOffset time) => "t=" + time.ToUnixTimeSeconds() + ",v1=" + Convert.ToHexString(
            System.Security.Cryptography.HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(time.ToUnixTimeSeconds() + "." + body)));
        Check(StripeWebhookSignature.Verify(webhookBody, Sign(webhookBody, signatureNow), signingSecret, signatureNow), "StripeWebhookSignatureShould.Accept_On_Verify_WhenSignatureValid");
        Check(!StripeWebhookSignature.Verify(webhookBody + " ", Sign(webhookBody, signatureNow), signingSecret, signatureNow), "StripeWebhookSignatureShould.Reject_On_Verify_WhenBodyTampered");
        Check(!StripeWebhookSignature.Verify(webhookBody, Sign(webhookBody, signatureNow.AddMinutes(-6)), signingSecret, signatureNow), "StripeWebhookSignatureShould.Reject_On_Verify_WhenTimestampStale");
        Check(!StripeWebhookSignature.Verify(webhookBody, Sign(webhookBody, signatureNow.AddMinutes(6)), signingSecret, signatureNow), "StripeWebhookSignatureShould.Reject_On_Verify_WhenTimestampInFuture");
        Check(!StripeWebhookSignature.Verify(webhookBody, Sign(webhookBody, signatureNow), "whsec_wrong", signatureNow), "StripeWebhookSignatureShould.Reject_On_Verify_WhenSecretWrong");
        Check(StripeWebhookSignature.Verify(webhookBody, "t=" + signatureNow.ToUnixTimeSeconds() + ",v1=nothex," + Sign(webhookBody, signatureNow), signingSecret, signatureNow), "StripeWebhookSignatureShould.Accept_On_Verify_WhenAnyV1SignatureMatches");
        var queuePath = Path.Combine(Path.GetTempPath(), "phoenix-email-" + Guid.NewGuid());
        var emailOptions = Options.Create(new PaymentEmailOptions { Enabled = true, WebhookSecret = signingSecret, Password = "fixture", QueueDirectory = queuePath });
        var emailQueue = new PaymentEmailQueue(emailOptions);
        var emailLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentEmailQueue>.Instance;
        async Task<int> Webhook(string body, bool signed = true, long? length = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            context.Request.ContentLength = length;
            if (signed) context.Request.Headers["Stripe-Signature"] = Sign(body, DateTimeOffset.UtcNow);
            var response = await PaymentWebhook.HandleAsync(context.Request, emailOptions, emailQueue, emailLogger, default);
            return ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)response).StatusCode ?? 200;
        }
        try
        {
            Check(await Webhook(webhookBody, length: 262145) == 413, "PaymentWebhookShould.Reject_On_HandleAsync_WhenDeclaredBodyTooLarge");
            Check(await Webhook(webhookBody.Replace("\"cs_test_12345678901\"", "null")) == 400,
                "PaymentWebhookShould.Reject_On_HandleAsync_WhenSessionIdNull");
            Check(await Webhook("{broken") == 400, "PaymentWebhookShould.Reject_On_HandleAsync_WhenJsonMalformed");
            Check(await Webhook(new string('x', 262145)) == 413, "PaymentWebhookShould.Reject_On_HandleAsync_WhenChunkedBodyTooLarge");
            Check(await Webhook(webhookBody.Replace("questions", "unrelated")) == 200 && !Directory.Exists(queuePath), "PaymentWebhookShould.Ignore_On_HandleAsync_WhenServiceUnrelated");
            Check(await Webhook(webhookBody.Replace("checkout.session.completed", "payment_intent.succeeded")) == 200 && !Directory.Exists(queuePath), "PaymentWebhookShould.Ignore_On_HandleAsync_WhenEventUnrelated");
            Check(await Webhook(webhookBody, false) == 400, "PaymentWebhookShould.Reject_On_HandleAsync_WhenUnsigned");
            Check(await Webhook(webhookBody.Replace("paid", "unpaid")) == 200 && !Directory.Exists(queuePath), "PaymentWebhookShould.Ignore_On_HandleAsync_WhenSessionUnpaid");
            Check(await Webhook(webhookBody) == 200, "PaymentWebhookShould.Persist_On_HandleAsync_WhenSessionPaid");
            Check(await Webhook(webhookBody) == 200 && Directory.GetFiles(queuePath, "*.json").Length == 1, "PaymentWebhookShould.SuppressDuplicate_On_HandleAsync_WhenSessionPending");
            var queued = File.ReadAllText(Directory.GetFiles(queuePath, "*.json").Single());
            Check(queued.Contains("reply@example.com") && queued.Contains("A question?"), "PaymentWebhookShould.IncludeFulfillmentDetails_On_HandleAsync_WhenReadingPaid");
            using (var gate = emailQueue.AcquireLease())
                Check(await Webhook(webhookBody.Replace("12345678901", "12345678903")) == 503,
                    "PaymentWebhookShould.RequestRetry_On_HandleAsync_WhenQueueLocked");
            var sender = new FakeEmailSender();
            var worker = new PaymentEmailWorker(emailQueue, sender, emailOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentEmailWorker>.Instance);
            await worker.ProcessAsync(default);
            Check(Directory.GetFiles(queuePath, "*.json").Length == 1, "PaymentEmailWorkerShould.PreservePending_On_ProcessAsync_WhenSmtpFails");
            sender.Fail = false;
            var restartedQueue = new PaymentEmailQueue(emailOptions);
            worker = new PaymentEmailWorker(restartedQueue, sender, emailOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentEmailWorker>.Instance);
            await worker.ProcessAsync(default);
            Check(sender.Sent == 1 && Directory.GetFiles(queuePath, "*.json").Length == 0, "PaymentEmailWorkerShould.RemoveCustomerData_On_ProcessAsync_AfterSmtpAcceptance");
            await Webhook(webhookBody.Replace("checkout.session.completed", "checkout.session.async_payment_succeeded"));
            await worker.ProcessAsync(default);
            Check(sender.Sent == 1, "PaymentWebhookShould.SuppressDuplicate_On_HandleAsync_WhenAnotherEventReferencesDeliveredSession");
            Check(await Webhook(webhookBody.Replace("12345678901", "12345678902").Replace("questions", "tip")) == 200,
                "PaymentWebhookShould.QueueTip_On_HandleAsync_WhenPaid");
            await worker.ProcessAsync(default);
            Check(sender.Sent == 2, "PaymentEmailWorkerShould.SendTip_On_ProcessAsync_WhenQueued");
            var corruptPath = Path.Combine(queuePath, "corrupt.json");
            File.WriteAllText(corruptPath, "{broken");
            await worker.ProcessAsync(default);
            Check(File.Exists(corruptPath) && sender.Sent == 2, "PaymentEmailWorkerShould.PreserveCorruptMessage_On_ProcessAsync_WithoutSending");
            Check(emailOptions.Value.IsSmtpConfigured, "PaymentEmailOptionsShould.AllowSmtp_On_IsSmtpConfigured_WhenMailboxReady");
            emailOptions.Value.Port = 25;
            Check(!emailOptions.Value.IsConfigured, "PaymentEmailOptionsShould.Reject_On_IsConfigured_WhenPortIsNotStartTls");
            emailOptions.Value.Port = 587;
            emailOptions.Value.Enabled = false;
            Check(await Webhook(webhookBody) == 503, "PaymentWebhookShould.FailClosed_On_HandleAsync_WhenDisabled");
        }
        finally { if (Directory.Exists(queuePath)) Directory.Delete(queuePath, true); }
        Console.WriteLine("PASS: webhook authentication, paid-only fulfillment details, durable queue, SMTP retry, and duplicate suppression.");
    }

    private sealed class FakeEmailSender : IPaymentEmailSender
    {
        public bool Fail { get; set; } = true;
        public int Sent { get; private set; }
        public Task SendAsync(PaymentEmail email, CancellationToken cancellationToken)
        {
            if (Fail) throw new IOException("Fixture SMTP failure");
            Sent++;
            return Task.CompletedTask;
        }
    }
}
