# DawnOfThePhoenix.Checks

This dependency-free executable runs C# checks against checkout validation, mocked Stripe responses, SMTP delivery retries, signed payment webhooks, safe return URLs, and public configuration. Paid-booking checks use mocked SMTP delivery and Twilio HTTP responses and an isolated temporary journal; they send no real messages or payments. It is not an xUnit/Shouldly project and is run with `dotnet run`, not `dotnet test`.

```sh
dotnet run --project tests/DawnOfThePhoenix.Checks -c Release
```

The optional native-JavaScript booking-dialog checks run with `node tests/browser/booking.mjs` from the repository root using Node.js 22 or newer. They use a minimal DOM double to check session selection, event URLs, official embed options, dark/copper styling, one-time script loading, stale-session protection, payment permission, and script-error/email fallback, without external calls or npm packages. Real browser/payment QA still follows `CAL-COM-SETUP.md`.

Use the SDK described in the [root README](../../README.md). For checks against the actual deployment package, see [published application checks](../deployment/README.md).

Booking activation checks verify that configured slugs and the website's Stripe key cannot activate an unverified session, each verified event is exposed independently, invalid paths remain withheld, and public configuration omits secrets. These checks verify the site's activation safeguard; they do not prove Cal.com's account settings require payment. Complete the account tests in `CAL-COM-SETUP.md` before enabling each verification flag.

## Test conventions

Use short, outcome-first names for new and changed tests; some older names vary:

```text
{DomainType}Should.{ExpectedBehavior}_On_{Member}_{Scenario}
EmploymentPeriodShould.ReturnNewValueAndPreserveOriginal_On_ReplaceExited_WhenDateIsValid
```

- Name classes `{DomainType}Should`; use PascalCase within phrases and underscores
  between them. Match the production member name when practical.
- Put observable behavior first, then a precise `When...` scenario if it adds
  value. Use a clearer connector such as `After` when needed.
- Test concrete types and one behavior at a time; avoid implementation details.
- Cover normalization, boundaries, equality, present/absent optional values, and
  invalid inputs. Assert unchanged aggregate state after failure and unchanged
  original values after immutable transformations.
- Cover collection duplicates, primary cardinality, and membership snapshots.
  Use theories for boundaries and fixed caller-supplied dates where supported.
- Add provider-backed round-trip and corrupt-row tests when aggregate persistence
  is introduced. Converter tests alone do not prove aggregate persistence works.

Use code, existing tests, and coverage reports to find gaps. Keep test guidance
about behavior and conventions rather than maintaining duplicate coverage lists
or historical test counts.
