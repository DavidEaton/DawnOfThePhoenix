using System.Text.Json;
using DawnOfThePhoenix.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DawnOfThePhoenix.Web.Pages.Api;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CheckoutStatusModel(StripeCheckoutService stripe) : PageModel
{
    public async Task<IActionResult> OnGetAsync([FromQuery(Name = "session_id")] string? sessionId, CancellationToken cancellationToken)
    {
        if (!stripe.IsConfigured) return Error("Payments are not configured.", 503);
        if (sessionId is null || !StripeCheckoutService.IsValidSessionId(sessionId)) return Error("Invalid checkout session", 400);
        try
        {
            var result = await stripe.GetStatusAsync(sessionId, cancellationToken);
            return new JsonResult(new { paid = result.Paid, kind = result.Kind });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or CheckoutUnavailableException)
        {
            return Error("Unable to verify payment. Please check your receipt or contact Dawn before paying again.", 502);
        }
    }
    private static JsonResult Error(string message, int status) => new(new { error = message }) { StatusCode = status };
}
