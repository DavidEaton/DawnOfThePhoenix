namespace DawnOfThePhoenix.Web.Options;

public sealed class CalComOptions
{
    public string WebhookSecret { get; set; } = "";
    public int ThirtyMinuteEventId { get; set; }
    public int SixtyMinuteEventId { get; set; }
    public string Username { get; set; } = "";
    public string ThirtyMinuteEvent { get; set; } = "";
    public string SixtyMinuteEvent { get; set; } = "";
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
    public string EmailTo { get; set; } = "";
    public string EmailFrom { get; set; } = "";
    public string SendGridApiKey { get; set; } = "";
    public string TwilioAccountSid { get; set; } = "";
    public string TwilioAuthToken { get; set; } = "";
    public string SmsFrom { get; set; } = "";
    public string SmsTo { get; set; } = "";
    public string StateDirectory { get; set; } = "";
}
