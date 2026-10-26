# Published application checks

These checks launch the actual Release publish output in Production mode, bind only to loopback on an OS-assigned port, and stop it when finished. They require Bash, curl, standard Unix text tools, and the .NET 10 ASP.NET Core runtime. Azure Pipelines runs them on its Ubuntu build agent before packaging.

```sh
dotnet publish DawnOfThePhoenix.Web -c Release -o /tmp/phoenix-publish
bash tests/deployment/smoke-publish.sh /tmp/phoenix-publish
```

The checks verify rendered pages, deployed CSS/JavaScript/favicon, the antiforgery token in the home page, secret-free public configuration, and rejection of a checkout POST without antiforgery credentials. The server gets a fake Stripe secret and a reserved test public URL. The rejected checkout makes no Stripe request or charge. The script uses no real account credentials.

Run the existing C# checks as well for checkout validation and mocked Stripe behavior:

```sh
dotnet run --project tests/DawnOfThePhoenix.Checks -c Release
```

These local checks do not verify Azure's TLS/proxy configuration, Azure authorization, or real Stripe/Cal.com transactions. Follow the post-deployment verification in [AZURE-SETUP.md](../../AZURE-SETUP.md).
