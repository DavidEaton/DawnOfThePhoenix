using System.Net.Mail;

namespace DawnOfThePhoenix.Web.Options;

public sealed class CalComOptions
{
    public string WebhookSecret { get; set; } = "";
    public int ThirtyMinuteEventId { get; set; }
    public int SixtyMinuteEventId { get; set; }
    public string Username { get; set; } = "";
    public string ThirtyMinuteEvent { get; set; } = "";
    public string SixtyMinuteEvent { get; set; } = "";
    // Operator attestation after testing required payment in the matching Cal.com event.
    public bool ThirtyMinutePaymentVerified { get; set; }
    public bool SixtyMinutePaymentVerified { get; set; }
}

public sealed class StripeOptions
{
    public string SecretKey { get; set; } = "";
}

public sealed class SiteOptions
{
    public string Email { get; set; } = "dawn@dawnofthephoenix.com";
    public string PublicBaseUrl { get; set; } = "";
}

public sealed class BookingAlertOptions
{
    public string TwilioAccountSid { get; set; } = "";
    public string TwilioAuthToken { get; set; } = "";
    public string SmsFrom { get; set; } = "";
    public string SmsTo { get; set; } = "";
    public string StateDirectory { get; set; } = "";
}

public sealed class PaymentEmailOptions
{
    public bool Enabled { get; set; }
    public string WebhookSecret { get; set; } = "";
    public string Host { get; set; } = "mail.privateemail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "dawn@dawnofthephoenix.com";
    public string Password { get; set; } = "";
    public string Recipient { get; set; } = "dawn@dawnofthephoenix.com";
    public string QueueDirectory { get; set; } = "";
    public bool IsConfigured => IsSmtpConfigured && WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal)
        && Path.IsPathFullyQualified(QueueDirectory);
    public bool IsSmtpConfigured => Enabled && !string.IsNullOrWhiteSpace(Password)
        && Port == 587 && !string.IsNullOrWhiteSpace(Host)
        && MailAddress.TryCreate(Username, out _) && MailAddress.TryCreate(Recipient, out _);
}
