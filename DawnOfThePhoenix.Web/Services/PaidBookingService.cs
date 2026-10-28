using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DawnOfThePhoenix.Web.Options;
using Microsoft.Extensions.Options;

namespace DawnOfThePhoenix.Web.Services;

public sealed class PaidBookingService(
    HttpClient client, IOptions<CalComOptions> calOptions, IOptions<BookingAlertOptions> alertOptions,
    IPaymentEmailSender emailSender, IOptions<PaymentEmailOptions> emailOptions)
{
    private readonly CalComOptions cal = calOptions.Value;
    private readonly BookingAlertOptions alerts = alertOptions.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(cal.WebhookSecret)
        && !string.IsNullOrWhiteSpace(cal.Username) && cal.ThirtyMinuteEventId > 0 && cal.SixtyMinuteEventId > 0
        && Path.IsPathFullyQualified(alerts.StateDirectory)
        && emailOptions.Value.IsSmtpConfigured
        && new[] { alerts.TwilioAccountSid,
            alerts.TwilioAuthToken, alerts.SmsFrom, alerts.SmsTo, alerts.StateDirectory }.All(v => !string.IsNullOrWhiteSpace(v));

    public bool Verify(byte[] body, string signature)
    {
        if (string.IsNullOrWhiteSpace(cal.WebhookSecret) || signature.Length != 64) return false;
        try
        {
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(cal.WebhookSecret), body);
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature));
        }
        catch (FormatException) { return false; }
    }

    // Only signed BOOKING_PAID messages reach this method. Ignore every other event.
    public async Task HandleAsync(byte[] body, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<CalWebhook>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException();
        if (message.TriggerEvent != "BOOKING_PAID") return;
        var booking = message.Payload ?? throw new JsonException();
        if (booking.Organizer?.Username != cal.Username
            || (booking.EventTypeId != cal.ThirtyMinuteEventId && booking.EventTypeId != cal.SixtyMinuteEventId)) return;
        if (string.IsNullOrWhiteSpace(booking.Uid) || booking.Uid.Length > 200 || booking.StartTime == default
            || booking.Attendees is not { Length: > 0 }) throw new JsonException();

        // Hash the identifier: no customer information or webhook body is stored on disk.
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(booking.Uid)));
        Directory.CreateDirectory(alerts.StateDirectory);
        var path = Path.Combine(alerts.StateDirectory, key);
        // The lock file remains on disk; closing the handle releases the lock after a crash too.
        await using var gate = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var time = booking.StartTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var duration = booking.EventTypeId == cal.ThirtyMinuteEventId ? 30 : 60;
        var name = string.Concat((booking.Attendees[0]?.Name ?? "Client").Where(c => !char.IsControl(c)).Take(100));
        var text = $"Paid {duration}-minute session with Dawn: {name}. Starts {time}. Booking: {booking.Uid}. Check Cal.com for details.";
        var failed = false;
        foreach (var channel in new[] { "email", "sms" })
        {
            var marker = path + "." + channel;
            if (File.Exists(marker)) continue;
            try
            {
                await SendAsync(channel, text, cancellationToken);
                await File.WriteAllTextAsync(marker + ".tmp", "accepted", cancellationToken);
                File.Move(marker + ".tmp", marker, overwrite: true);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or SmtpException)
            {
                failed = true; // Still attempt the other channel; return 503 so Cal.com can retry.
            }
        }
        if (failed) throw new HttpRequestException("Booking alert provider unavailable.");
    }

    private async Task SendAsync(string channel, string text, CancellationToken cancellationToken)
    {
        if (channel == "email")
        {
            await emailSender.SendAsync(new PaymentEmail("cal-booking", "Paid session booked — DawnOfThePhoenix", text), cancellationToken);
            return;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(alerts.TwilioAccountSid)}/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{alerts.TwilioAccountSid}:{alerts.TwilioAuthToken}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = alerts.SmsFrom, ["To"] = alerts.SmsTo, ["Body"] = text
        });
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Booking alert provider unavailable.");
    }

    public sealed record CalWebhook(string? TriggerEvent, PaidBooking? Payload);
    public sealed record PaidBooking(string? Uid, int EventTypeId, DateTimeOffset StartTime,
        Organizer? Organizer, Attendee[]? Attendees);
    public sealed record Organizer(string? Username);
    public sealed record Attendee(string? Name);
}
