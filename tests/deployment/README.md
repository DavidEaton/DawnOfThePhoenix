# Published application checks

These checks launch the actual Release publish output in Production mode, bind only to loopback on an OS-assigned port, and stop it when finished. They require Bash, curl, standard Unix text tools, and the .NET 10 ASP.NET Core runtime. Azure Pipelines runs them on its Ubuntu build agent before packaging.

```sh
dotnet publish DawnOfThePhoenix.Web -c Release -o /tmp/phoenix-publish
bash tests/deployment/smoke-publish.sh /tmp/phoenix-publish
```

The checks verify rendered pages, deployed CSS/JavaScript/favicon, versioned SVG and ICO icon links, the root bookmark ICO response (content type, published bytes, and four-frame file header), the antiforgery token in the home page, secret-free public configuration, and rejection of a checkout POST without antiforgery credentials. The server gets a fake Stripe secret and a reserved test public URL. The rejected checkout makes no Stripe request or charge. The script uses no real account credentials. Fake Cal.com and alert-provider configuration verifies public event links, omission of notification secrets/phone, and rejection of unsigned Cal.com (401) and Stripe (400) webhooks without a browser antiforgery token. It checks that rejection creates no delivery journal or queued email; no provider request is made.

Tip-jar checks verify the closed native disclosure, its tooltip and glyph, enclosure of all payment content and notes, the supplied Cash App recipient, exact PayPal-issued receiving-code destination (and rejection of merchant-checkout links), optional-tip wording, open amounts, no guessed Venmo link, and both published QR image responses. Run the checks again without Stripe to verify that the direct payment methods remain available while the card button is hidden:

```sh
SMOKE_STRIPE_SECRET='' bash tests/deployment/smoke-publish.sh /tmp/phoenix-publish
```

For deterministic QR image validation, use `python tools/generate-tip-qr.py --check` in the optional QR tooling environment described in the [root README](../../README.md). It checks the committed module patterns against freshly generated codes for the documented destinations. This does not verify provider account ownership or complete a payment.

Run the existing C# checks as well for checkout validation and mocked Stripe behavior:

```sh
dotnet run --project tests/DawnOfThePhoenix.Checks -c Release
```

These local checks do not verify Azure's TLS/proxy configuration, Azure authorization, or real Stripe/Cal.com transactions. Follow the post-deployment verification in [AZURE-SETUP.md](../../AZURE-SETUP.md).
