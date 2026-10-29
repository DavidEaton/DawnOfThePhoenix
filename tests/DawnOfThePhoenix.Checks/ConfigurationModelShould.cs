using System.Text.Json;
using DawnOfThePhoenix.Web.Options;
using DawnOfThePhoenix.Web.Pages;
using Microsoft.Extensions.Options;

internal static class ConfigurationModelShould
{
    public static void Run()
    {
        WithholdBookingLinks_On_Get_WhenPaymentsAreNotVerified();
        ExposeOnlyVerifiedSessions_On_Get_RegardlessOfWebsiteStripeKey();
        WithholdInvalidPaths_On_Get_WhenVerified();
        OmitSecrets_On_Get_WhenPaymentsAreVerified();
        Console.WriteLine("PASS: booking payment activation, independent session readiness, safe paths, and private configuration.");
    }

    private static CalComOptions Events() => new()
    {
        Username = "dawn", ThirtyMinuteEvent = "30-minute-reading", SixtyMinuteEvent = "60-minute-reading"
    };

    private static JsonElement Config(CalComOptions cal, string stripeKey = "")
    {
        var page = new ConfigurationModel(Options.Create(cal),
            Options.Create(new StripeOptions { SecretKey = stripeKey }), Options.Create(new SiteOptions()));
        var script = page.OnGet().Content!;
        using var json = JsonDocument.Parse(script["window.PHOENIX_CONFIG = ".Length..^1]);
        return json.RootElement.Clone();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void WithholdBookingLinks_On_Get_WhenPaymentsAreNotVerified()
    {
        var config = Config(Events(), "fixture-written-reading-key");
        Check(config.GetProperty("thirtyMinuteUrl").GetString() == "", "A slug and website Stripe key must not activate 30-minute bookings.");
        Check(config.GetProperty("sixtyMinuteUrl").GetString() == "", "A slug and website Stripe key must not activate 60-minute bookings.");
        Check(config.GetProperty("bookingUrl").GetString() == "", "The generic profile must not bypass payment verification.");
        Check(config.GetProperty("paymentsReady").GetBoolean(), "Written-reading/card-tip readiness must remain independent.");
    }

    private static void ExposeOnlyVerifiedSessions_On_Get_RegardlessOfWebsiteStripeKey()
    {
        foreach (var thirty in new[] { false, true })
        foreach (var hour in new[] { false, true })
        foreach (var stripeKey in new[] { "", "fixture-written-reading-key" })
        {
            var cal = Events();
            cal.ThirtyMinutePaymentVerified = thirty;
            cal.SixtyMinutePaymentVerified = hour;
            var config = Config(cal, stripeKey);
            Check(config.GetProperty("thirtyMinuteUrl").GetString() == (thirty ? "https://cal.com/dawn/30-minute-reading" : ""), "30-minute activation must apply only to its event.");
            Check(config.GetProperty("sixtyMinuteUrl").GetString() == (hour ? "https://cal.com/dawn/60-minute-reading" : ""), "60-minute activation must apply only to its event.");
            Check(config.GetProperty("bookingUrl").GetString() == "", "Verified events must not expose a generic profile fallback.");
        }
    }

    private static void WithholdInvalidPaths_On_Get_WhenVerified()
    {
        foreach (var invalid in new[] { "", "../bad", "event?free=true", "https://other.example/event" })
        {
            var cal = Events();
            cal.ThirtyMinutePaymentVerified = cal.SixtyMinutePaymentVerified = true;
            cal.Username = invalid;
            var config = Config(cal);
            Check(config.GetProperty("thirtyMinuteUrl").GetString() == "" && config.GetProperty("sixtyMinuteUrl").GetString() == "", "Invalid usernames must withhold both events.");
            cal.Username = "dawn";
            cal.ThirtyMinuteEvent = invalid;
            config = Config(cal);
            Check(config.GetProperty("thirtyMinuteUrl").GetString() == "" && config.GetProperty("sixtyMinuteUrl").GetString() == "https://cal.com/dawn/60-minute-reading", "An invalid 30-minute slug must not disable a valid 60-minute event.");
            cal.ThirtyMinuteEvent = "30-minute-reading";
            cal.SixtyMinuteEvent = invalid;
            config = Config(cal);
            Check(config.GetProperty("sixtyMinuteUrl").GetString() == "" && config.GetProperty("thirtyMinuteUrl").GetString() == "https://cal.com/dawn/30-minute-reading", "An invalid 60-minute slug must not disable a valid 30-minute event.");
        }
    }

    private static void OmitSecrets_On_Get_WhenPaymentsAreVerified()
    {
        var cal = Events();
        cal.ThirtyMinutePaymentVerified = cal.SixtyMinutePaymentVerified = true;
        cal.WebhookSecret = "fixture-cal-secret";
        var config = Config(cal, "fixture-stripe-secret").GetRawText();
        Check(!config.Contains("fixture-cal-secret") && !config.Contains("fixture-stripe-secret"), "Public configuration must omit server secrets.");
    }
}
