using System.Text.Json;
using DawnOfThePhoenix.Web.Options;
using DawnOfThePhoenix.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DawnOfThePhoenix.Web.Pages.Api;

[RequestSizeLimit(2048)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CheckoutModel(StripeCheckoutService stripe, IOptions<SiteOptions> siteOptions) : PageModel
{
    public IActionResult OnGet() => Error("Method not allowed", 405);

    // Razor Pages validates the antiforgery token before this handler executes.
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!stripe.IsConfigured) return Error("Online payments are being set up. Please contact Dawn.", 503);
        if (Request.ContentLength > 2048) return Error("Request too large", 413);
        CheckoutRequest? input;
        try
        {
            input = await JsonSerializer.DeserializeAsync<CheckoutRequest>(Request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);
        }
        catch (JsonException) { return Error("Invalid request", 400); }
        catch (BadHttpRequestException) { return Error("Request too large", 413); }
        if (input?.Kind is not ("questions" or "tip")) return Error("Unknown service", 400);
        var amount = input.Kind == "questions" ? 800 : input.Amount;
        if (amount is null or < 100 or > 100000 || (input.Kind == "tip" && amount % 100 != 0))
            return Error("Please choose a whole-dollar tip from $1 to $1,000.", 400);

        var baseUrl = siteOptions.Value.PublicBaseUrl.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = $"{Request.Scheme}://{Request.Host}";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin)
            || (origin.Scheme != "https" && !(origin.Scheme == "http" && origin.IsLoopback))
            || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            return Error("The payment return address needs configuration. Please contact Dawn.", 503);
        try
        {
            var url = await stripe.CreateAsync(input.Kind, amount.Value, origin.GetLeftPart(UriPartial.Authority), cancellationToken);
            return new JsonResult(new { url });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or CheckoutUnavailableException)
        {
            return Error("Checkout is temporarily unavailable. Please contact Dawn to arrange payment.", 502);
        }
    }

    private static JsonResult Error(string message, int status) => new(new { error = message }) { StatusCode = status };
    public sealed record CheckoutRequest(string? Kind, int? Amount);
}
