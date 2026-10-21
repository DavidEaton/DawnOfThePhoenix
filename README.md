# PhoenixRising — ASP.NET Core Razor Pages

Dawn's psychic medium business website, built in the existing **ASP.NET Core 10 Razor Pages** application. The design, artwork, prices, Cal.com booking dialog, Stripe checkout, and tip jar are preserved. Node.js, npm, Wrangler, and a JavaScript build step are not required. The small browser script uses plain JavaScript.

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

Checkout is implemented server-side in C# with `HttpClient`; the secret never appears in public configuration. Razor Pages validates an antiforgery cookie/token on checkout requests. The server fixes the written reading price at $8 and validates whole-dollar tips from $1 to $1,000. Return-page payment status is checked against Stripe.

Written readings collect three required yes/no questions (150 characters each) and a reply email in Stripe Checkout. Dawn fulfills them manually by viewing the paid Checkout session/custom fields in Stripe, then replying within 48 hours of payment. This app does not automate the readings or order emails. Enable customer receipts and merchant payment notifications in Stripe. Set the public business terms URL in Stripe to your website's `/terms` page before using Checkout terms consent. Stripe's live payment activation is required before real purchases.

Tips are optional; they do not purchase a reading or reserve a session. Live session purchases use Cal.com's Stripe integration rather than the written-reading checkout endpoint.

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

- `Pages/Index.cshtml`: the home page and service sections.
- `Pages/Shared/_Layout.cshtml`: shared branding, navigation, metadata, footer, and antiforgery token.
- `Pages/Terms.cshtml`, `Pages/Privacy.cshtml`: reading terms and privacy.
- `Pages/Configuration.cshtml.cs`: safe public Cal.com/payment readiness settings at `/config.js`.
- `Pages/Api/`: Razor Page handlers for checkout and payment status.
- `Services/StripeCheckoutService.cs`: server-side Stripe integration.
- `wwwroot/`: plain CSS/JavaScript and optimized supplied images.

The superseded Node/Worker application has been removed. Supplied files were reference material; the user's current copy and prices govern the site. No invented testimonials or follower counts are included.

## Checks

```sh
dotnet build DawnOfThePhoenix.slnx
dotnet run --project tests/DawnOfThePhoenix.Checks
```

The dependency-free C# checks mock Stripe's HTTP responses; they make no charges. They cover request/amount validation, the fixed $8 price, required question fields, tip bounds, error redaction, safe redirects, payment status, and secret-free public configuration. Local HTTP smoke checks also verified Razor rendering, assets, routes, and antiforgery rejection. Live Stripe/Cal.com transactions still require the accounts to be configured; visual browser QA remains unverified in this environment.
