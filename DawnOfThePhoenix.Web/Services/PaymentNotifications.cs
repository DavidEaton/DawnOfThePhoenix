using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DawnOfThePhoenix.Web.Options;
using Microsoft.Extensions.Options;

namespace DawnOfThePhoenix.Web.Services;

public static class StripeWebhookSignature
{
    public static bool Verify(string body, string header, string secret, DateTimeOffset now)
    {
        var parts = header.Split(',').Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToArray();
        var timestamp = parts.FirstOrDefault(x => x[0] == "t")?[1];
        if (!long.TryParse(timestamp, out var seconds) || seconds < now.ToUnixTimeSeconds() - 300
            || seconds > now.ToUnixTimeSeconds() + 300 || string.IsNullOrEmpty(secret)) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + body));
        foreach (var part in parts.Where(x => x[0] == "v1"))
        {
            try
            {
                if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(part[1]))) return true;
            }
            catch (FormatException) { }
        }
        return false;
    }
}

public sealed record PaymentEmail(string SessionId, string Subject, string Body);

public sealed class PaymentEmailQueue(IOptions<PaymentEmailOptions> options)
{
    public string DirectoryPath => options.Value.QueueDirectory;
    public void Enqueue(PaymentEmail email)
    {
        Directory.CreateDirectory(DirectoryPath);
        // Hash the Stripe ID rather than using untrusted text as a path.
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.SessionId)));
        var pending = Path.Combine(DirectoryPath, key + ".json");
        var sent = Path.Combine(DirectoryPath, key + ".sent");
        // Serialize webhook writes with the worker; deploy on one App Service instance.
        using var lease = AcquireLease();
        if (File.Exists(pending) || File.Exists(sent)) return;
        var temporary = Path.Combine(DirectoryPath, Guid.NewGuid() + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(email));
            File.Move(temporary, pending);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public FileStream AcquireLease() => new(Path.Combine(DirectoryPath, "queue.lock"), FileMode.OpenOrCreate,
        FileAccess.ReadWrite, FileShare.None);
}

public interface IPaymentEmailSender
{
    Task SendAsync(PaymentEmail email, CancellationToken cancellationToken);
}

public sealed class PrivateEmailSender(IOptions<PaymentEmailOptions> options) : IPaymentEmailSender
{
    public async Task SendAsync(PaymentEmail email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var message = new MailMessage(settings.Username, settings.Recipient, email.Subject, email.Body);
        using var smtp = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true, // STARTTLS is required; no plaintext fallback.
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.Username, settings.Password)
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}

public sealed class PaymentEmailWorker(PaymentEmailQueue queue, IPaymentEmailSender sender,
    IOptions<PaymentEmailOptions> options, ILogger<PaymentEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
        {
            if (options.Value.Enabled && (!string.IsNullOrWhiteSpace(options.Value.WebhookSecret)
                || !string.IsNullOrWhiteSpace(options.Value.QueueDirectory))) logger.LogError("Payment email is enabled but configuration is incomplete; webhook requests will return 503.");
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (IOException) { logger.LogWarning("Payment email queue unavailable or busy; retrying in one minute."); }
            catch (Exception) { logger.LogError("Payment email delivery failed; queued notification will retry in one minute. Check SMTP settings and queue storage."); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(queue.DirectoryPath);
        using var lease = queue.AcquireLease();
        foreach (var path in Directory.EnumerateFiles(queue.DirectoryPath, "*.json").Take(20))
        {
            try
            {
                if (File.Exists(Path.ChangeExtension(path, ".sent")))
                {
                    File.Delete(path);
                    continue;
                }
                var email = JsonSerializer.Deserialize<PaymentEmail>(await File.ReadAllTextAsync(path, cancellationToken))
                    ?? throw new JsonException();
                await sender.SendAsync(email, cancellationToken);
                // Retain only the deduplication marker, not customer data, after delivery.
                File.WriteAllText(Path.ChangeExtension(path, ".sent"), "");
                File.Delete(path);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { logger.LogError("A payment notification could not be delivered; retained for retry."); }
        }
    }
}

public static class PaymentWebhook
{
    public static async Task<IResult> HandleAsync(HttpRequest request, IOptions<PaymentEmailOptions> options,
        PaymentEmailQueue queue, ILogger<PaymentEmailQueue> logger, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) return Results.StatusCode(503);
        const int limit = 262144;
        if (request.ContentLength > limit) return Results.StatusCode(413);
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (bytes.Length + count > limit) return Results.StatusCode(413);
            bytes.Write(buffer, 0, count);
        }
        var body = Encoding.UTF8.GetString(bytes.ToArray());
        if (!StripeWebhookSignature.Verify(body, request.Headers["Stripe-Signature"].ToString(), settings.WebhookSecret, DateTimeOffset.UtcNow))
            return Results.BadRequest();
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded")) return Results.Ok();
            var session = root.GetProperty("data").GetProperty("object");
            if (session.GetProperty("payment_status").GetString() != "paid") return Results.Ok();
            if (!session.TryGetProperty("metadata", out var metadata) || !metadata.TryGetProperty("service", out var service)) return Results.Ok();
            var kind = service.GetString();
            if (kind is not ("questions" or "tip")) return Results.Ok();
            var id = session.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(id) || !StripeCheckoutService.IsValidSessionId(id)) return Results.BadRequest();
            var amount = session.GetProperty("amount_total").GetInt64();
            var currency = session.GetProperty("currency").GetString();
            var displayAmount = currency == "usd"
                ? (amount / 100m).ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + " USD"
                : $"{amount} {currency} (minor currency units)";
            var text = new StringBuilder($"A payment was received for {(kind == "questions" ? "a written reading" : "a tip")}.\nAmount: {displayAmount}\nStripe Checkout session: {id}\n");
            if (session.TryGetProperty("customer_details", out var customer) && customer.ValueKind == JsonValueKind.Object
                && customer.TryGetProperty("email", out var email)) text.AppendLine($"Customer reply email: {email.GetString()}");
            if (kind == "questions" && session.TryGetProperty("custom_fields", out var fields))
                foreach (var field in fields.EnumerateArray())
                    if (field.TryGetProperty("text", out var answer) && answer.TryGetProperty("value", out var value))
                        text.AppendLine($"{field.GetProperty("key").GetString()}: {value.GetString()}");
            text.AppendLine("\nConfirm details in your Stripe dashboard. Written readings are fulfilled manually within 48 hours.");
            queue.Enqueue(new PaymentEmail(id, kind == "questions" ? "Phoenix: paid written reading" : "Phoenix: tip received", text.ToString()));
            return Results.Ok();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return Results.BadRequest(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("Could not persist a payment notification; returning 503 so Stripe retries.");
            return Results.StatusCode(503);
        }
    }
}
