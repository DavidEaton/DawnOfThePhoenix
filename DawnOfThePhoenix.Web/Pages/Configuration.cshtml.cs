using System.Text.Json;
using System.Text.RegularExpressions;
using DawnOfThePhoenix.Web.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DawnOfThePhoenix.Web.Pages;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed partial class ConfigurationModel(
    IOptions<CalComOptions> calOptions,
    IOptions<StripeOptions> stripeOptions,
    IOptions<SiteOptions> siteOptions) : PageModel
{
    public ContentResult OnGet()
    {
        var cal = calOptions.Value;
        var user = SafeSlug(cal.Username);
        var root = user.Length > 0 ? $"https://cal.com/{user}" : "";
        var thirty = SafeSlug(cal.ThirtyMinuteEvent);
        var hour = SafeSlug(cal.SixtyMinuteEvent);
        var config = new
        {
            // Never offer the profile as a fallback: it may list unpaid event types.
            bookingUrl = "",
            thirtyMinuteUrl = cal.ThirtyMinutePaymentVerified && root.Length > 0 && thirty.Length > 0 ? $"{root}/{thirty}" : "",
            sixtyMinuteUrl = cal.SixtyMinutePaymentVerified && root.Length > 0 && hour.Length > 0 ? $"{root}/{hour}" : "",
            paymentsReady = !string.IsNullOrWhiteSpace(stripeOptions.Value.SecretKey),
            email = siteOptions.Value.Email
        };
        return Content("window.PHOENIX_CONFIG = " + JsonSerializer.Serialize(config) + ";", "application/javascript; charset=utf-8");
    }

    private static string SafeSlug(string value) => SlugPattern().IsMatch(value) ? value : "";

    [GeneratedRegex(@"\A[a-zA-Z0-9_-]{1,100}\z")]
    private static partial Regex SlugPattern();
}
