# Cal.com paid sessions and Dawn’s alerts

The existing Razor Pages app embeds Cal.com event pages in the booking dialog. Customers choose the 30-minute ($30 USD) or 60-minute ($55 USD) session, select availability in their time zone, supply booking details, and pay through **Cal.com’s Stripe integration**. If an embedded payment method cannot run in the browser, the dialog has a link to the same event page. A missing event configuration offers email contact instead of another event or an unpriced profile page. The website’s separate Stripe secret serves written readings and tips only.

## Account setup

1. Sign in at https://app.cal.com/. Connect Dawn’s calendar and configure her actual availability, time zone, buffers, meeting location, cancellation/refund policy, and booking confirmations.
2. Create the two individual, non-seated event types. Connect Stripe in Cal.com and **require payment at booking**: 30 minutes for $30 USD and 60 minutes for $55 USD. Do not use a payment-later or free event. Verify payment availability for the account’s current Cal.com plan.
3. Copy the real username, event slugs, and numeric event type IDs from Cal.com. Do not guess them. Set `CalCom:Username`, `CalCom:ThirtyMinuteEvent`, `CalCom:SixtyMinuteEvent`, `CalCom:ThirtyMinuteEventId`, and `CalCom:SixtyMinuteEventId`. The IDs scope alerts; the slugs select the embedded pages. Keep each pair aligned when changing an event. Changing only the site’s prices does not change Cal.com’s charges.
4. Configure SendGrid with a verified sender and a restricted Mail Send API key. Configure an SMS-capable Twilio sender and complete any registration required for the destination country. Trial accounts may require verified recipients. Use Dawn’s actual mobile number and email as recipients. These alerts go to Dawn, not the customer.
5. Configure the values below in ASP.NET user secrets for development or App Service environment variables for production. Keep credentials out of Git and chat. No Cal.com API key is required.
6. In Cal.com **Settings → Developer → Webhooks**, create an enabled webhook pointing to `https://YOUR-SITE/api/cal/webhook`. Select **Booking Paid** (`BOOKING_PAID`) only, and set its signing secret to the same random secret stored as `CalCom:WebhookSecret`. Use Cal.com’s default JSON payload, without a custom template. The receiver follows the documented `2021-10-20` payload fields; those fields remain supported in the `2026-07-27` version. Do not substitute a booking-created or payment-initiated trigger for a successful payment.

## Configuration

Environment variable names below use `__`; user secrets use `:` instead.

| Environment variable | Purpose |
| --- | --- |
| `CalCom__Username` | Dawn’s actual username |
| `CalCom__ThirtyMinuteEvent`, `CalCom__SixtyMinuteEvent` | Real paid-event slugs |
| `CalCom__ThirtyMinuteEventId`, `CalCom__SixtyMinuteEventId` | Positive numeric IDs for those events |
| `CalCom__WebhookSecret` | Random secret matching the Cal.com webhook secret |
| `BookingAlerts__EmailTo` | Dawn’s notification email |
| `BookingAlerts__EmailFrom` | SendGrid verified sender address |
| `BookingAlerts__SendGridApiKey` | Secret SendGrid Mail Send key |
| `BookingAlerts__TwilioAccountSid` | Twilio account SID |
| `BookingAlerts__TwilioAuthToken` | Secret Twilio auth token |
| `BookingAlerts__SmsFrom` | SMS-capable Twilio sender in E.164 format |
| `BookingAlerts__SmsTo` | Dawn’s mobile in E.164 format, including `+` and country code |
| `BookingAlerts__StateDirectory` | Private durable writable directory outside the deployment and web root; for the documented Linux App Service, `/home/data/phoenix-booking-alerts` |

Do not set state storage under `wwwroot`, the deployment package, or temporary storage. Restrict filesystem access to the application operator. Use **one App Service instance**, preserve this directory during restarts, deployments, and rollback, and back it up. Before scaling out or running multiple slots against these webhooks, replace the filesystem journal with a shared transactional store/outbox. Do not delete accepted markers to retry another channel. The journal retains hashed booking references and channel acceptance markers; no webhook bodies, questions, or customer details are stored there. Retain markers for the lifetime of bookings/webhook replay to avoid duplicate alerts. Configure outbound HTTPS access to `api.sendgrid.com` and `api.twilio.com` in the production host.

## Delivery behavior and operations

The server validates the SHA-256 HMAC over the **exact raw request bytes** using `x-cal-signature-256` before parsing JSON. Only `BOOKING_PAID` for the configured organizer and either event ID can send alerts. Browser callbacks, unpaid bookings, abandoned checkouts, cancellations, and written readings/tips cannot trigger these alerts. The rest of checkout retains browser antiforgery protection.

Each channel receives the client name, session duration, start time explicitly labeled **UTC**, and booking reference. Dawn can check the booking in Cal.com for contact and meeting details. Neither notification includes reading notes or card data. No provider keys, destination phone, or alert configuration is added to `/config.js`.

A successful response is `204` after both providers accept their messages, or for an ignored event. An invalid signature returns `401`, invalid paid payload `400`, oversized payload `413` (64 KiB limit), and incomplete configuration, locked journal, storage/provider failure `503`. The receiver attempts both channels even if one provider fails and records successful acceptance per channel; replaying a delivery retries only unfinished channels, including after an application restart. Concurrent delivery of the same booking receives `503` while another request holds its lock.

Monitor failed Cal.com deliveries and the application’s generic paid-alert warnings. Retry/replay failed deliveries through Cal.com after fixing the provider, credentials, or storage; do not assume unlimited automatic retries. Check SendGrid delivery/bounce logs and Twilio message delivery logs for actual inbox/handset delivery. A provider’s HTTP acceptance does not guarantee delivery. There is no background retry worker. If a provider accepts a message but the connection times out or the process stops before the journal is written, a replay can send a duplicate; this is at-least-once delivery, not an exactly-once guarantee. Keep native Cal.com booking emails enabled as a separate booking record.

## Before enabling customer bookings

- Complete a test payment for **each** duration through the site dialog. Confirm Cal.com’s exact price/currency, time zone, Stripe payment, calendar entry, customer confirmation, and Dawn’s email and SMS. Test on mobile as well as desktop, including the external-page fallback. Use a test account/event setup, not unintended live charges.
- Abandon or fail payment and confirm no paid-session alert. Reschedule/cancel and confirm the calendar behavior and absence of another paid alert.
- Replay the same paid webhook and confirm no additional alerts. Temporarily fail one provider, confirm the other still receives its alert and the receiver returns `503`, restore it, and replay to confirm only the failed channel sends.
- Restart/redeploy and replay to confirm state persists. Verify missing/invalid signatures cannot send alerts, and public configuration contains no secrets or phone number.
- Switch to the intended live account/payment setup only after these checks. This repository does not provision Cal.com, Stripe, SendGrid, Twilio, or Azure accounts. Empty defaults do not activate live scheduling or alerts.

## References

- [Cal.com webhook triggers, payloads and HMAC verification](https://cal.com/docs/developing/guides/automation/webhooks)
- [SendGrid Mail Send API](https://www.twilio.com/docs/sendgrid/api-reference/mail-send/mail-send)
- [Twilio Message resource](https://www.twilio.com/docs/messaging/api/message-resource)
