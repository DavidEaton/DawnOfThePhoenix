using System.Text.Json;
using DawnOfThePhoenix.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DawnOfThePhoenix.Web.Pages.Api;

// Cal.com cannot supply a browser antiforgery token. Authenticate the raw bytes with HMAC instead.
[IgnoreAntiforgeryToken]
[RequestSizeLimit(MaxBodyBytes)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CalWebhookModel(PaidBookingService bookings, ILogger<CalWebhookModel> logger) : PageModel
{
    private const int MaxBodyBytes = 65536;
    public IActionResult OnGet() => StatusCode(405);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!bookings.IsConfigured) return StatusCode(503);
        if (Request.ContentLength > MaxBodyBytes) return StatusCode(413);
        try
        {
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (body.Length + read > MaxBodyBytes) return StatusCode(413);
                body.Write(buffer, 0, read);
            }
            var bytes = body.ToArray();
            if (!bookings.Verify(bytes, Request.Headers["x-cal-signature-256"].ToString())) return StatusCode(401);
            await bookings.HandleAsync(bytes, cancellationToken);
            return StatusCode(204);
        }
        catch (JsonException) { return StatusCode(400); }
        catch (BadHttpRequestException) { return StatusCode(413); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            // Do not log provider responses, credentials, or customer details.
            logger.LogWarning("Paid booking alert could not complete. Check provider delivery logs and replay the Cal.com webhook.");
            return StatusCode(503);
        }
    }
}
