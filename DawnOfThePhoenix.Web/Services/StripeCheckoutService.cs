using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using DawnOfThePhoenix.Web.Options;
using Microsoft.Extensions.Options;

namespace DawnOfThePhoenix.Web.Services;

public sealed partial class StripeCheckoutService(HttpClient client, IOptions<StripeOptions> options)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.SecretKey);

    public async Task<string> CreateAsync(string kind, int amount, string baseUrl, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = baseUrl + "/?checkout=complete&session_id={CHECKOUT_SESSION_ID}",
            ["cancel_url"] = baseUrl + "/?checkout=cancelled",
            ["payment_method_types[0]"] = "card",
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = "usd",
            ["line_items[0][price_data][unit_amount]"] = amount.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = kind == "questions"
                ? "PhoenixRising · Three yes/no questions" : "A little love for Dawn · Tip",
            ["metadata[service]"] = kind,
            ["payment_intent_data[metadata][service]"] = kind,
            ["consent_collection[terms_of_service]"] = "required",
            ["custom_text[terms_of_service_acceptance][message]"] =
                "Readings are for entertainment purposes only. No outcome is guaranteed. Terms: " + baseUrl + "/terms"
        };
        if (kind == "questions")
        {
            fields["line_items[0][price_data][product_data][description]"] =
                "Three yes-or-no questions, answered by Dawn within 48 hours of payment. Enter your questions and reply email at checkout.";
            for (var i = 0; i < 3; i++)
            {
                fields[$"custom_fields[{i}][key]"] = $"question{i + 1}";
                fields[$"custom_fields[{i}][label][type]"] = "custom";
                fields[$"custom_fields[{i}][label][custom]"] = $"Yes/no question {i + 1}";
                fields[$"custom_fields[{i}][type]"] = "text";
                fields[$"custom_fields[{i}][text][maximum_length]"] = "150";
            }
        }
        using var request = AuthorizedRequest(HttpMethod.Post, "checkout/sessions");
        request.Content = new FormUrlEncodedContent(fields);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new CheckoutUnavailableException();
        using var data = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!data.RootElement.TryGetProperty("url", out var value)
            || !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var url)
            || url.Scheme != "https" || url.Host != "checkout.stripe.com")
            throw new CheckoutUnavailableException();
        return url.AbsoluteUri;
    }

    public async Task<PaymentStatus> GetStatusAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var request = AuthorizedRequest(HttpMethod.Get, "checkout/sessions/" + Uri.EscapeDataString(sessionId));
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new CheckoutUnavailableException();
        using var data = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = data.RootElement;
        var paid = root.TryGetProperty("payment_status", out var payment) && payment.GetString() == "paid";
        string? kind = null;
        if (root.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty("service", out var service))
            kind = service.GetString() is "questions" or "tip" ? service.GetString() : null;
        return new PaymentStatus(paid, kind);
    }

    public static bool IsValidSessionId(string id) => SessionPattern().IsMatch(id);

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.SecretKey);
        request.Headers.Add("Stripe-Version", "2025-02-24.acacia");
        return request;
    }

    [GeneratedRegex(@"\Acs_(test_|live_)?[A-Za-z0-9]{10,250}\z")]
    private static partial Regex SessionPattern();
}

public sealed record PaymentStatus(bool Paid, string? Kind);
public sealed class CheckoutUnavailableException : Exception;
