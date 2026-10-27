# PhoenixRising — ASP.NET Core Razor Pages

Dawn's psychic medium business website, built in the existing **ASP.NET Core 10 Razor Pages** application. The design, artwork, prices, Cal.com booking dialog, Stripe checkout, and tip jar are preserved. Node.js, npm, Wrangler, and a JavaScript build step are not required. The browser code uses native JavaScript modules with no bundler or package installation.

## Run locally

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then:

```sh
git clone https://github.com/DavidEaton/DawnOfThePhoenix.git
cd DawnOfThePhoenix
dotnet run --project DawnOfThePhoenix.Web --launch-profile http
```

Open **http://localhost:5240**. You can also open `DawnOfThePhoenix.slnx` in a compatible Visual Studio version and run the web project. Changes to Razor, CSS, and JavaScript can be developed with:

```sh
dotnet watch --project DawnOfThePhoenix.Web run --launch-profile http
```

The email fallback works without account credentials. Live booking and payment activation requires Dawn's Cal.com and Stripe accounts.

## Connect Cal.com

Create Dawn's account at https://cal.com/signup, connect her calendar, and set timezone, availability, buffers, meeting location/video provider, and confirmation details. Create:

- **30-minute reading:** $30 USD, suggested slug `30-minute-reading`.
- **60-minute reading:** $55 USD, suggested slug `60-minute-reading`.

Connect Stripe within Cal.com and require payment on each event. Verify Cal.com's current plan supports your chosen payment features. Live session availability, bookings, timezones, and payments are handled by Cal.com. The site embeds those event pages with an external-page fallback; no Cal.com API key is needed.

Set the real username and event slugs in `DawnOfThePhoenix.Web/appsettings.json`, or use development user secrets:

```sh
dotnet user-secrets set "CalCom:Username" "your-username" --project DawnOfThePhoenix.Web
dotnet user-secrets set "CalCom:ThirtyMinuteEvent" "30-minute-reading" --project DawnOfThePhoenix.Web
dotnet user-secrets set "CalCom:SixtyMinuteEvent" "60-minute-reading" --project DawnOfThePhoenix.Web
```

## Connect Stripe

Create and activate Dawn's account at https://dashboard.stripe.com/register. Dawn must complete business and payout verification herself. Start with a **test-mode secret key** and save it locally with user secrets:

```sh
dotnet user-secrets set "Stripe:SecretKey" "YOUR_STRIPE_TEST_SECRET_KEY" --project DawnOfThePhoenix.Web
```

Do not commit real keys to `appsettings.json`, a `.dev.vars` file, Git, or chat. This app uses ASP.NET configuration, not `.dev.vars`.

Checkout is implemented server-side in C# with `HttpClient`; the secret never appears in public configuration. Razor Pages validates an antiforgery cookie/token on checkout requests. The server fixes the written reading price at $30 and validates whole-dollar tips from $1 to $1,000. Return-page payment status is checked against Stripe.

Written readings collect three required yes/no questions (150 characters each) and a reply email in Stripe Checkout. Dawn fulfills them manually by viewing the paid Checkout session/custom fields in Stripe, then replying within 48 hours of payment. This app does not automate the readings or order emails. Enable customer receipts and merchant payment notifications in Stripe. Set the public business terms URL in Stripe to your website's `/terms` page before using Checkout terms consent. Stripe's live payment activation is required before real purchases.

Tips are optional; they do not purchase a reading or reserve a session. Live session purchases use Cal.com's Stripe integration rather than the written-reading checkout endpoint.

## Cash App and PayPal tip jar

The home page's `Pages/Home/_TipJar.cshtml` partial displays tappable payment links and locally hosted QR codes. They work without JavaScript or a Stripe key:

- Cash App: **Dawn Phoenix**, **$22PhoenixRising888**, linked to `https://cash.app/$22PhoenixRising888`.
- PayPal: **Dawn Eaton**, **@Risingphoenix888**, using Dawn’s official PayPal receiving-code link: `https://www.paypal.com/qrcodes/p2pqrc/XBDN5S58CNNYQ`. The button and QR image contain this exact URL, with no preset amount. The supplied email remains a contact label; payment routing comes from PayPal’s own receiving code. Do not replace it with an email-derived merchant checkout URL or guess a PayPal.Me address.

Venmo is omitted until Dawn supplies an official payment/profile link. Do not invent a recipient or use a placeholder payment destination. When Stripe is configured, **Tip by card** remains available and asks for an amount without a preselected value. Direct-provider tips do not create Stripe sessions or appear in Stripe payment history. Verify them in the relevant provider account.

The QR codes contain the same HTTPS URLs as the buttons; they contain no amount, login credentials, or tracking redirect. The browser/provider decides whether to open its app or a web page. They use black modules on white with a four-module quiet zone and error correction M. No external QR service is contacted when the page loads.

To regenerate after a recipient/link change, use optional development tooling (not required to build, run, or deploy the site):

```sh
python -m venv /tmp/phoenix-qr-tools
/tmp/phoenix-qr-tools/bin/python -m pip install qrcode==8.2
/tmp/phoenix-qr-tools/bin/python tools/generate-tip-qr.py
/tmp/phoenix-qr-tools/bin/python tools/generate-tip-qr.py --check
```

Update the destinations in `tools/generate-tip-qr.py`, the button links/recipient labels in the partial, and the relevant deployment checks together. Scan both codes and confirm the intended recipient and editable amount on actual phones before accepting tips; don't complete test payments accidentally. The supplied public email remains the contact link for questions.

## Azure deployment

See [the Azure App Service and Azure DevOps walkthrough](AZURE-SETUP.md) for the Linux App Service setup, federated service connection, application settings, pipeline, and rollback steps. Azure DevOps deploys successful `main` builds; the legacy GitHub deployment workflow is manual-only.

## Publish to a .NET host

```sh
dotnet publish DawnOfThePhoenix.Web -c Release -o publish
```

Deploy to a host supporting ASP.NET Core 10 (for example, Azure App Service, IIS with the matching hosting bundle, or a Linux .NET host). Use HTTPS. The earlier Sites-hosted review draft is a separate deployment; this Razor Pages app requires .NET hosting.

Configure these values using the host's configuration/secret settings. Environment variables use `__` in place of `:`:

| Configuration key | Environment variable | Value |
| --- | --- | --- |
| `CalCom:Username` | `CalCom__Username` | Actual username |
| `CalCom:ThirtyMinuteEvent` | `CalCom__ThirtyMinuteEvent` | Actual 30-minute event slug |
| `CalCom:SixtyMinuteEvent` | `CalCom__SixtyMinuteEvent` | Actual 60-minute event slug |
| `Stripe:SecretKey` | `Stripe__SecretKey` | Live secret, stored securely |
| `Site:PublicBaseUrl` | `Site__PublicBaseUrl` | Canonical HTTPS origin, e.g. `https://your-domain.com` |
| `Site:Email` | `Site__Email` | Dawn's contact email |
| `AllowedHosts` | `AllowedHosts` | Your actual production hostnames |

`Site:PublicBaseUrl` determines Stripe's return and terms URLs. Locally, it may be blank to use localhost automatically. Behind a reverse proxy, configure trusted forwarded headers/HTTPS termination for your host. Do not disable antiforgery validation to compensate for a proxy configuration problem.

Test both live session event types, their calendar entries and confirmation emails, the written-reading questions/email in Stripe test mode, tip checkout, cancellations, and payment status before accepting customers. Dawn should review privacy, reading terms, and cancellation/refund policies. The entertainment disclaimer does not guarantee legal protection.

## Project structure

All paths below are relative to `DawnOfThePhoenix.Web/`.

| What you want to edit | Where to look |
| --- | --- |
| Home-page section order | `Pages/Index.cshtml` |
| Hero, readings, Dawn’s introduction, gifts, FAQs, and other section content | Named partials in `Pages/Home/` |
| Header, footer, and dialog shell | `Pages/Shared/_Header.cshtml`, `_Footer.cshtml`, `_Dialog.cshtml` |
| Shared metadata, CSS/script loading, and antiforgery token | `Pages/Shared/_Layout.cshtml` |
| Reading terms and privacy page | `Pages/Terms.cshtml`, `Pages/Privacy.cshtml` |
| Colors, typography, section styling, responsive rules | `wwwroot/css/site.css` (grouped with section comments) |
| Browser startup | `wwwroot/js/site.js` |
| Cal.com booking and email booking fallback | `wwwroot/js/booking.js` |
| Written-reading UI and email questions | `wwwroot/js/readings.js` |
| Cash App / PayPal tip links and QR cards | `Pages/Home/_TipJar.cshtml` |
| Stripe tip amount dialog | `wwwroot/js/tips.js` |
| QR images and regeneration | `wwwroot/assets/tips/`, root `tools/generate-tip-qr.py` |
| Payment API calls, redirects, and return-page verification | `wwwroot/js/checkout.js` |
| Public browser configuration and URL/email helpers | `wwwroot/js/configuration.js` |
| Shared dialog, navigation, and privacy popup | `wwwroot/js/dialog.js`, `wwwroot/js/layout.js` |
| Server-side configuration options | `Options/BusinessOptions.cs` and the settings described above |
| Secret-free `/config.js` response | `Pages/Configuration.cshtml.cs` |
| Checkout request validation and payment status handlers | `Pages/Api/` |
| Server-side Stripe HTTP requests | `Services/StripeCheckoutService.cs` |

`site.js` imports the feature modules directly in the browser. The server-provided `/config.js` is loaded first and contains only public booking paths, email, and payment readiness. Stripe keys remain server-side. Razor partials contain section markup; they do not make booking or payment requests.

The tab icon source is `wwwroot/assets/favicon.svg`. The root `wwwroot/favicon.ico` supplies the same artwork to bookmark managers and browsers requesting `/favicon.ico` directly. Keep both in sync when changing the icon; the ICO contains 16, 32, 48, and 64-pixel frames. The layout versions both icon links for cache refresh. Existing bookmarks may retain their cached icon until the page is revisited or the bookmark is recreated after deployment.

For a content change, edit the relevant partial. For a styling change, edit the matching section in `site.css`. For booking behavior, start in `booking.js`; for payment transport, start in `checkout.js`. The repository’s `.editorconfig` sets four-space indentation and consistent line endings. Formatting and organization do not introduce a Node.js build step.

The superseded Node/Worker application has been removed. Supplied files were reference material; the user's current copy and prices govern the site. No invented testimonials or follower counts are included.

## Checks

```sh
dotnet build DawnOfThePhoenix.slnx
dotnet run --project tests/DawnOfThePhoenix.Checks
```

The dependency-free C# checks mock Stripe's HTTP responses; they make no charges. They cover request/amount validation, the fixed $30 price, required question fields, tip bounds, error redaction, safe redirects, payment status, and secret-free public configuration. Local HTTP smoke checks also verified Razor rendering, assets, routes, and antiforgery rejection. Live Stripe/Cal.com transactions still require the accounts to be configured; visual browser QA remains unverified in this environment.
