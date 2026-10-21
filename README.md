# DawnOfThePhoenix / PhoenixRising

The PhoenixRising website built for Dawn is in [`phoenixrising/`](phoenixrising/). It includes the website design, optimized supplied images, Cal.com scheduling integration, server-side Stripe Checkout for written readings and tips, and setup documentation.

See [account and deployment setup](phoenixrising/README.md) for activation steps. Real bookings and payments require Dawn's Cal.com and Stripe accounts to be configured.

Build the website Worker bundle with Node.js 20 or newer:

```sh
cd phoenixrising
node scripts/build.mjs
```

The output is `phoenixrising/dist/server/index.js`. The public files can be previewed statically, but Stripe Checkout and runtime configuration require the Worker server. The current hosting manifest identifies the existing Sites project.

`DawnOfThePhoenix.Web/` contains the original ASP.NET Core starter. The PhoenixRising implementation is a separate JavaScript/Worker application; running the .NET starter does not serve it.

Never commit production credentials. Configure `STRIPE_SECRET_KEY` using secure hosting settings.
