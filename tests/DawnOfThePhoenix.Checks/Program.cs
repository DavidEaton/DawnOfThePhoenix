using System.Net;
using System.Text;
using System.Text.Json;
using DawnOfThePhoenix.Web.Options;
using DawnOfThePhoenix.Web.Pages;
using DawnOfThePhoenix.Web.Pages.Api;
using DawnOfThePhoenix.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

await PaidBookingServiceShould.RunAsync();
await PaymentWebhookShould.RunAsync();

var handler = new FakeStripeHandler();
var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.stripe.com/v1/") };
var stripe = new StripeCheckoutService(client, Options.Create(new StripeOptions { SecretKey = "test-fixture-only" }));
var emptyStripe = new StripeCheckoutService(client, Options.Create(new StripeOptions()));
var site = Options.Create(new SiteOptions());

CheckoutModel Checkout(string body, StripeCheckoutService? service = null, long? length = null)
{
    var context = new DefaultHttpContext();
    context.Request.Scheme = "http";
    context.Request.Host = new HostString("localhost:5240");
    context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
    context.Request.ContentLength = length ?? Encoding.UTF8.GetByteCount(body);
    return new CheckoutModel(service ?? stripe, site) { PageContext = new PageContext { HttpContext = context } };
}
static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static int Status(IActionResult result) => ((JsonResult)result).StatusCode ?? 200;

Check(Status(await Checkout("{}", emptyStripe).OnPostAsync(default)) == 503, "Unconfigured payment must fail closed.");
Check(Status(await Checkout("{bad").OnPostAsync(default)) == 400, "Malformed JSON must be rejected.");
Check(Status(await Checkout("{\"kind\":\"other\"}").OnPostAsync(default)) == 400, "Unknown service must be rejected.");
foreach (var amount in new[] { -1, 0, 99, 101, 100001 })
    Check(Status(await Checkout($"{{\"kind\":\"tip\",\"amount\":{amount}}}").OnPostAsync(default)) == 400, "Invalid tip must be rejected.");
Check(Status(await Checkout("{\"kind\":\"tip\",\"amount\":100.5}").OnPostAsync(default)) == 400, "Fractional cents must be rejected.");
Check(Status(await Checkout("{}", length: 3000).OnPostAsync(default)) == 413, "Large payload must be rejected.");

var result = await Checkout("{\"kind\":\"questions\",\"amount\":1}").OnPostAsync(default);
Check(Status(result) == 200, "Valid written reading must create checkout.");
Check(handler.Fields["line_items[0][price_data][unit_amount]"] == "800", "Client cannot override reading price.");
Check(handler.Fields["custom_fields[2][type]"] == "text", "Three question fields must be collected.");
Check(handler.Fields["custom_fields[0][text][maximum_length]"] == "150", "Question limit must be enforced by checkout.");
Check(handler.Fields["success_url"].StartsWith("http://localhost:5240/"), "Local payment return URL must use .NET origin.");
Check(handler.Fields["consent_collection[terms_of_service]"] == "required", "Reading terms consent is required.");
Check(handler.Authorization == "Bearer test-fixture-only", "Stripe requests must use server credentials.");
await Checkout("{\"kind\":\"tip\",\"amount\":1000}").OnPostAsync(default);
Check(handler.Fields["line_items[0][price_data][unit_amount]"] == "1000", "Tip amount must reach Stripe.");

handler.ResponseJson = "{\"url\":\"https://evil.example/checkout\"}";
Check(Status(await Checkout("{\"kind\":\"questions\"}").OnPostAsync(default)) == 502, "Unsafe checkout destination must be rejected.");
handler.ResponseStatus = HttpStatusCode.BadRequest;
handler.ResponseJson = "{\"error\":{\"message\":\"private-upstream-detail\"}}";
var error = await Checkout("{\"kind\":\"questions\"}").OnPostAsync(default);
Check(Status(error) == 502 && !JsonSerializer.Serialize(((JsonResult)error).Value).Contains("private-upstream-detail"), "Upstream errors must be redacted.");
handler.ResponseStatus = HttpStatusCode.OK;
handler.ResponseJson = "{\"payment_status\":\"paid\",\"metadata\":{\"service\":\"questions\"},\"customer_details\":{\"email\":\"private@example.com\"}}";
var statusPage = new CheckoutStatusModel(stripe);
Check(Status(await statusPage.OnGetAsync("bad", default)) == 400, "Invalid session ID must be rejected.");
var payment = (JsonResult)await statusPage.OnGetAsync("cs_test_12345678901", default);
var safePayment = JsonSerializer.Serialize(payment.Value);
Check(safePayment.Contains("\"paid\":true") && !safePayment.Contains("private@example.com"), "Payment verification must not return customer details.");
var configPage = new ConfigurationModel(Options.Create(new CalComOptions { Username = "dawn", ThirtyMinuteEvent = "30-minute-reading", WebhookSecret = "private-cal-secret", ThirtyMinuteEventId = 30, SixtyMinuteEventId = 60 }), Options.Create(new StripeOptions { SecretKey = "test-fixture-only" }), site);
var config = configPage.OnGet().Content!;
Check(config.Contains("https://cal.com/dawn/30-minute-reading") && !config.Contains("test-fixture-only") && !config.Contains("private-cal-secret"), "Public configuration must expose booking paths and omit secrets.");
var badConfig = new ConfigurationModel(Options.Create(new CalComOptions { Username = "../bad" }), Options.Create(new StripeOptions()), site).OnGet().Content!;
Check(!badConfig.Contains("../bad"), "Cal.com identifiers must be validated.");
Console.WriteLine("PASS: C# checkout validation, fixed pricing, question fields, tips, error redaction, Stripe verification, safe redirects, and private configuration.");

sealed class FakeStripeHandler : HttpMessageHandler
{
    public Dictionary<string, string> Fields { get; private set; } = [];
    public string? Authorization { get; private set; }
    public string ResponseJson { get; set; } = "{\"url\":\"https://checkout.stripe.com/c/pay/test_fixture\"}";
    public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Authorization = request.Headers.Authorization?.ToString();
        if (request.Content is not null)
        {
            var form = await request.Content.ReadAsStringAsync(cancellationToken);
            Fields = form.Split('&').Select(part => part.Split('=', 2)).ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1].Replace('+', ' ')));
        }
        return new HttpResponseMessage(ResponseStatus) { Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json") };
    }
}
