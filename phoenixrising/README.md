# PhoenixRising — Dawn's website

Modern responsive website with uploaded phoenix artwork, Dawn's real photograph, service pricing, spiritual-gift exploration, FAQs, entertainment disclaimer, Cal.com scheduling integration, Stripe Checkout, and optional tips.

Live private review URL: https://phoenixrising-dawn.sharphinny12.chatgpt.site

## Account setup required to activate scheduling and payments

The site code is built. Both merchant accounts still need to be created by Dawn. It cannot receive real bookings or payments until the following setup is complete. Never put passwords or secret keys in chat, public JavaScript, Git, or documents.

### 1. Create Dawn's Cal.com account

Sign up at https://cal.com/signup and connect Dawn's calendar. Set her timezone, working hours, meeting location/video provider, buffers, and availability. Connect the Stripe app in Cal.com after completing Stripe setup.

Create two one-on-one event types:
- A moment of clarity: 30 minutes, $30 USD, suggested event slug `30-minute-reading`.
- A deeper connection: 60 minutes, $55 USD, suggested event slug `60-minute-reading`.

Enable Cal.com's payment requirement for each event, choose USD, and enter the corresponding price. Add relevant booking questions, confirmation details, and cancellation/rescheduling policy. Verify whether Cal.com's current plan supports the desired payment integration. Test calendar conflict handling and confirmation emails before launch.

The website uses the hosted Cal.com booking interface, embedded in a dialog with an external-page fallback. Cal.com handles live availability, timezone selection, confirmation and session payment. No Cal.com API key is required for this integration.

### 2. Create Dawn's Stripe account

Sign up at https://dashboard.stripe.com/register. Dawn must provide the real business and payout details required by Stripe and complete activation. Enable customer receipts and merchant payment notifications. Add the site's `/terms` URL in Stripe's public business details before using Checkout's terms consent.

The website's server creates Stripe Checkout sessions for:
- Three yes/no questions: fixed $8 USD. Checkout collects the three required questions (150 characters each) and customer email.
- Tips: a visitor-selected whole-dollar amount from $1 to $1,000 USD.

Customer card details are handled by Stripe. The site does not store card details. Prices are checked by the server. Payment confirmation queries Stripe and does not trust a success URL alone.

Dawn fulfills paid written readings manually: view the paid Checkout session, its three custom fields, and customer email in Stripe, then reply within 48 hours of payment. This implementation does not send automated readings or order emails. A tip does not reserve a session.

### 3. Configure the hosted site

Add these runtime values using the site's environment settings (not `public/config.js`):

| Key | Value | Secret? |
| --- | --- | --- |
| `CALCOM_USERNAME` | Dawn's actual Cal.com username | No |
| `CALCOM_30_EVENT` | Actual 30-minute event slug | No |
| `CALCOM_60_EVENT` | Actual 60-minute event slug | No |
| `STRIPE_SECRET_KEY` | Dawn's Stripe secret API key | Yes |

Use test mode first. Store the Stripe key directly in the hosting secret settings through an appropriate secure flow; do not paste it into chat. Redeploy the saved Site version after changing runtime settings so the new revision applies.

Until configured, booking buttons and the tip jar explain that online setup is pending and open email drafts to the supplied business-card address, dawn@dawnofthephoenix.com. The interim three-question form opens an email draft; the visitor must send it. It does not collect payment or silently submit questions.

### 4. Validate and launch

Complete a Cal.com test booking for each event type and confirm the correct price, video details, calendar entry, and emails. Complete a Stripe test checkout for a written reading and a tip, verify questions and contact email in Stripe, and check cancellation behavior. Then activate live payment settings and test again as appropriate. New Sites remain owner-private until sharing is changed to allow visitors. Select the intended public audience before sharing on TikTok. A custom domain can be connected afterward.

Dawn should review the reading terms, privacy text, and her cancellation/refund policy before inviting customers. The entertainment disclaimer describes the service; it does not guarantee legal protection.

## Source

- `public/`: website source, images, styles and browser interactions.
- `worker/api.mjs`: server-side checkout and safe public configuration.
- `scripts/build.mjs`: dependency-free build; embeds site assets into the Worker ESM bundle.
- `.openai/hosting.json`: existing Site project identity; preserve it.
- `dist/server/index.js`: generated deployment bundle.

Build with `node scripts/build.mjs` from the project root. No installation is needed. Keep production secrets out of source.

## Verification

Passed JavaScript syntax checks, static link/asset checks, DOM interaction checks, API tests with mocked Stripe responses, price and amount checks, cross-origin rejection, error redaction, public-config secret filtering, verified-payment return flow checks, and Worker route/bundle checks. Browser preview infrastructure was unavailable, so visual browser QA remains unverified. Live Stripe and Cal.com flows cannot be verified until Dawn's accounts are configured.

Supplied files were used as reference material. Current user instructions govern copy and prices. Old Etsy prompts and QR placeholders were not treated as instructions to carry out unrelated work. Uploaded non-business information and transaction screenshots are excluded from the published site.
