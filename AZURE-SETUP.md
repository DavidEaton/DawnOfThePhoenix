# Azure App Service and Azure DevOps setup

This repository is an ASP.NET Core **.NET 10 Razor Pages** application. No Node.js build, database migration, or container is required. These steps deploy the .NET repository, not the older Sites-hosted draft.

Target URL: https://dawnofthephoenix-ahdph6bwbrdvame6.centralus-01.azurewebsites.net

The prepared `azure-pipelines.yml` restores dependencies, builds Release, runs the existing executable application checks, smoke-checks the published application, publishes a deployment ZIP, and deploys successful `main` builds. Pull requests only build and run checks. The checks are a console application, so this pipeline intentionally runs `dotnet run`, not `dotnet test`.

## 1. Check the App Service

In **Azure Portal → App Services → your app → Overview**, note:

- **Name**: copy the actual resource name. The YAML assumes `dawnofthephoenix`; verify it. Do not use the full hostname or infer the resource name from its generated suffix.
- Subscription and resource group.
- Operating system: **Linux** or **Windows**.

Under **Settings → Configuration → General settings / Stack settings** (the portal may place these under **Configuration (preview)**):

- Use **.NET 10** / **.NET 10 LTS**, with managed code hosting rather than a custom container.
- For Linux, set startup command to `dotnet DawnOfThePhoenix.Web.dll` if needed. Leave Windows startup command empty; publishing generates `web.config`.
- Enable **HTTPS Only**. Enable **Always On** if your plan supports it.

If .NET 10 is unavailable for this app/region, resolve runtime availability before deploying this framework-dependent package; an older runtime cannot run it. Installing the SDK in the build agent does not install the runtime on App Service.

This app redirects HTTP to HTTPS in production. Azure terminates TLS at its front end. On Linux App Service, configure the platform-supported forwarded-header integration (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`) when required for the app to recognize the original HTTPS scheme. Use this only behind the trusted App Service front end; for other proxy topologies, configure known proxies/networks explicitly in the application. Verify a request to the HTTPS URL does not loop through redirects.

## 2. Create an Azure DevOps project and connect the repository

1. Open https://dev.azure.com and create or select an organization.
2. Create a private project, for example **DawnOfThePhoenix**.
3. Choose where the source lives:
   - **Keep GitHub:** use the existing `DavidEaton/DawnOfThePhoenix` repository and authorize the Azure Pipelines GitHub app for that repository during pipeline creation.
   - **Use Azure Repos:** open **Repos → Files → Import repository** and import the GitHub clone URL. A private source requires authentication. After import, make future commits to Azure Repos; the import is a snapshot, not continuous synchronization with GitHub.
4. Put `azure-pipelines.yml` at the repository root on the branch you intend to use. The supplied trigger uses `main`. If your production branch has a different name, update the CI trigger, PR filter, and Deploy stage's `refs/heads/main` condition together.

The existing GitHub Actions deployment workflow is now **manual-only**. Use Azure DevOps for automatic deployments so the two systems do not race to overwrite production. Do not manually run the legacy deployment during an Azure DevOps release.

## 3. Create the Azure service connection

In **Azure DevOps → Project settings → Service connections → New service connection**:

1. Select **Azure Resource Manager**.
2. Choose **App registration (automatic)** with **Workload identity federation**, or the equivalent automatic federated-identity option.
3. Select the subscription and scope to the app's resource group.
4. Name the connection **azure-dawnofthephoenix**, matching the YAML.
5. Save. Authorize just this pipeline when prompted instead of granting every pipeline access.

Federation avoids storing a client secret or publish profile. Creating the identity and role assignment requires suitable Microsoft Entra and Azure RBAC permissions. If automatic setup is unavailable, your administrator can create the identity and scoped role assignment and use the manual workload-federation option. An existing Azure subscription alone does not guarantee these permissions.

The deployment identity needs permission to deploy to the App Service; **Website Contributor** scoped to that app is a suitable starting role. Automatic setup may grant **Contributor** at the selected resource-group scope; review and narrow the role/scope afterward if appropriate. Your app does not need Azure credentials in its own environment variables for deployment.

## 4. Configure the application in Azure

Open **App Service → Settings → Environment variables → App settings** (older UI: **Configuration → Application settings**). Add the values below, then apply/save:

| Name | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Site__PublicBaseUrl` | `https://dawnofthephoenix-ahdph6bwbrdvame6.centralus-01.azurewebsites.net` |
| `AllowedHosts` | `dawnofthephoenix-ahdph6bwbrdvame6.centralus-01.azurewebsites.net` |
| `Site__Email` | Dawn's actual contact email |
| `CalCom__Username` | Dawn's actual Cal.com username |
| `CalCom__ThirtyMinuteEvent` | Actual 30-minute event slug |
| `CalCom__SixtyMinuteEvent` | Actual 60-minute event slug |
| `CalCom__ThirtyMinutePaymentVerified` | `false` until required payment is verified for the 30-minute event; then `true` |
| `CalCom__SixtyMinutePaymentVerified` | `false` until required payment is verified for the 60-minute event; then `true` |
| `Stripe__SecretKey` | Stripe **test-mode** secret key for initial verification |

Double underscores map to nested ASP.NET configuration. Do not commit the Stripe key or put it in YAML. User secrets from your development computer do not deploy. App Service settings remain in Azure across code deployments. A Key Vault reference is an option for managing the Stripe secret centrally.

The website can render without payment/booking credentials, using its existing unavailable/email fallback behavior. Calendar links are withheld for each session until its payment-verification flag is explicitly enabled. Follow [Payment before confirmation](CAL-COM-SETUP.md#payment-before-confirmation) before enabling either flag, including on an existing deployment: configured event slugs alone no longer activate online scheduling. These flags attest to manual verification; they do not modify Cal.com or protect its direct URLs. Reset the affected flag and reverify after changing account/event/payment settings. Cal.com handles live-session bookings; the server-side Stripe key handles written readings and tips. They are separate setups. Configure Stripe Checkout terms consent using this site's `/terms` URL.

For paid-session email and SMS alerts, also configure all values and the Cal.com webhook in [CAL-COM-SETUP.md](CAL-COM-SETUP.md). Use `/home/data/phoenix-booking-alerts` for `BookingAlerts__StateDirectory` on Linux App Service, outside the ZIP deployment, and keep the app at one instance until a shared transactional delivery journal is implemented. Verify Namecheap SMTP acceptance/inbox delivery and Twilio delivery logs and preserve the journal across deployments/rollback.

When adding a custom domain later, bind it and its HTTPS certificate in Azure, change `Site__PublicBaseUrl` to that canonical HTTPS origin, and add the domain to `AllowedHosts` (semicolon-separated when keeping multiple hostnames).

## 5. Create and run the pipeline

1. In Azure DevOps, open **Pipelines → Environments** and create **dawnofthephoenix-production**, resource type **None**. This is a deployment history/approval object; it does not create another Azure app.
2. Open **Pipelines → New pipeline**, choose **GitHub** or **Azure Repos Git**, and select your repository.
3. Select **Existing Azure Pipelines YAML file**, then `/azure-pipelines.yml` on your production branch.
4. Verify `webAppName` matches the actual Azure resource name, and `azureServiceConnection` matches the connection name.
5. The YAML's `appType` parameter defaults to **webAppLinux**. If the App Service is Windows, change its default to **webApp** before committing so automatic runs use the correct OS. This parameter does not change the App Service OS.
6. Save and run on `main`. Authorize the service connection and environment for this pipeline when Azure DevOps prompts.
7. Wait for both **Build** and **Deploy** stages to succeed.

If the run reports no hosted parallelism or stays queued because none is available, check **Organization settings → Parallel jobs**. Request the hosted-agent grant if eligible, purchase capacity, or configure a self-hosted agent with the required tools. A new DevOps organization may not immediately have hosted execution capacity.

GitHub PR builds use the YAML `pr` trigger. For **Azure Repos**, set **Repos → Branches → main → Branch policies → Build validation** to this pipeline; Azure Repos does not use the YAML `pr` trigger. Protect `main` so changes pass validation before merging. Merging to `main` then builds and deploys automatically.

Optionally add a manual release approval under **Pipelines → Environments → dawnofthephoenix-production → Approvals and checks**. This pauses production deployments after the package is built. The supplied YAML does not require an approval by itself.

## 6. Verify the first release

Open the Azure URL in a private/incognito browser window:

- Check the home page, images, CSS, JavaScript, `/terms`, and `/privacy`.
- Confirm HTTPS loads without a redirect loop.
- Verify both Cal.com paid event types, customer confirmations, calendar entries, and Dawn’s email and SMS alerts. Complete the payment-failure and webhook replay checks in `CAL-COM-SETUP.md`.
- Complete written-reading and tip checkout using Stripe test mode; check paid, cancelled, and failed/invalid paths and return URLs.
- Confirm the expected price, required questions, and reply email in Stripe before switching to a live secret.

For failures, enable **App Service logs → Application logging**, then inspect **Log stream** and **Diagnose and solve problems**. Typical causes are a missing .NET 10 runtime, a wrong app resource name, unauthorized service connection, missing settings, or incorrect forwarded-header handling.

For an initial CI/CD check, make a small content change on a feature branch, open a pull request, verify validation, then merge to `main` and confirm the new content appears on Azure.

Each build retains a `webapp` artifact with `webapp.zip`. To roll back code, rerun the Deploy stage of a known-good retained build. That restores its package, not older App Service settings. The first setup deploys directly to production; a staging slot with validation and swap can be added on an App Service plan supporting slots.

## Reference

- [Azure DevOps: deploy to Azure App Service](https://learn.microsoft.com/azure/devops/pipelines/targets/webapp?view=azure-devops)
- [Azure Resource Manager service connections](https://learn.microsoft.com/azure/devops/pipelines/library/connect-to-azure?view=azure-devops)
- [AzureWebApp task](https://learn.microsoft.com/azure/devops/pipelines/tasks/reference/azure-web-app-v1?view=azure-pipelines)
- [Configure ASP.NET Core in Azure App Service](https://learn.microsoft.com/azure/app-service/configure-language-dotnetcore)

Run `bash tests/deployment/smoke-publish.sh /absolute/path/to/publish` after `dotnet publish` to verify the deployment output locally. See `tests/deployment/README.md` for prerequisites and coverage.

The pipeline has been prepared locally. Azure resource settings, service connections, repository publishing, and an actual deployment must still be completed in your accounts.

## Payment notification emails through Namecheap Private Email

The app can notify Dawn about paid written readings and card tips through the existing `dawn@dawnofthephoenix.com` mailbox. No SendGrid/Plunk account or separate email subscription is needed. Existing Namecheap mailbox charges and sending limits still apply. This address is used only for notification sending/receiving; the public site contact address is unchanged.

Add these **App Service environment variables** (never commit passwords):

| Name | Value |
| --- | --- |
| `PaymentEmail__Enabled` | `true` |
| `PaymentEmail__Host` | `mail.privateemail.com` |
| `PaymentEmail__Port` | `587` |
| `PaymentEmail__Username` | `dawn@dawnofthephoenix.com` |
| `PaymentEmail__Recipient` | `dawn@dawnofthephoenix.com` |
| `PaymentEmail__Password` | A Private Email mailbox application password, or the mailbox password if application passwords are unavailable; not your Namecheap account password |
| `PaymentEmail__WebhookSecret` | Stripe endpoint signing secret (`whsec_...`) |
| `PaymentEmail__QueueDirectory` | `/home/data/payment-emails` on Linux App Service; use an absolute persistent path under `%HOME%` on Windows, expanded to its actual value |

SMTP always requires STARTTLS, certificate validation, and authentication on port 587. Namecheap's configuration reference: https://www.namecheap.com/support/knowledgebase/article.aspx/1179/2175/general-private-email-configuration-for-mail-clients-and-mobile-devices/

In Stripe's webhook/event-destination settings, add your canonical HTTPS origin plus `/api/stripe/webhook`, subscribing to `checkout.session.completed` and `checkout.session.async_payment_succeeded`. Copy that endpoint's signing secret into the setting above. Test and live endpoints have separate secrets; use the matching mode and Stripe account. A paid Checkout session must contain this app's `service=questions` or `service=tip` metadata. Unpaid, unrelated, and unsigned events do not trigger email. Notifications do not depend on the customer returning to the website.

The webhook acknowledges only after persisting a notification. A background worker checks the queue every minute, retaining failed messages for retry and recording successful Checkout session IDs to suppress repeated webhook deliveries. Customer details and reading questions appear in plain-text notification emails; they are removed from queue storage after SMTP acceptance, leaving an empty deduplication marker. Restrict access to and back up persistent storage. Do not put the queue under the deployed code or `wwwroot`. Keep App Service storage persistent and enable Always On for timely retries. Use one App Service instance, consistent with the Cal.com journal. Before scaling out or sharing slots, replace filesystem state with a shared transactional outbox. Do not prune `.sent` markers unless you accept replayed notifications.

Delivery is at least once: a crash or SMTP timeout after the server accepts an email can cause a duplicate on retry. SMTP acceptance does not guarantee inbox placement. Monitor App Service logs for delivery failures and pending `.json` files; a failed notification does not reverse or block a payment. Secrets and customer content are omitted from application error logs. The webhook returns 503 when email configuration or storage is unavailable so Stripe can retry within its retry window.

Before accepting live orders, complete a test-mode written reading and tip, confirm the email contents and sender/recipient, resend the same Stripe event to verify deduplication, and check that a failed SMTP attempt remains queued and succeeds after fixing configuration. Verify mailbox SPF/DKIM and spam-folder placement. Keep Stripe's merchant notifications enabled as a fallback until this is verified. Direct PayPal/Cash App tips are not covered. Cal.com bookings use the separate signed webhook described in CAL-COM-SETUP.md, with the same SMTP mailbox.
