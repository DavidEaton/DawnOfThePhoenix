# Host PhoenixRising on Azure with Azure DevOps

This guide uses GitHub as the source repository, Azure DevOps Services for CI/CD, and Azure App Service on Linux with .NET 10 for hosting. No Docker image, database, Node.js installation, or separate frontend hosting is needed for the current application.

Flow: feature branch → GitHub pull request → build/checks → merge into `main` → build/checks/package → App Service deployment → website smoke check.

## 1. Accounts and names

You need an Azure subscription, an Azure DevOps organization, and permission to administer the GitHub repository. Creating the Azure service connection automatically also requires sufficient Azure/Microsoft Entra permissions to create its identity and assign access. Azure subscription Owner is the usual starting point for a personal subscription.

Suggested resource names:

| Item | Suggested name |
| --- | --- |
| Azure resource group | `rg-phoenixrising-prod` |
| App Service plan | `asp-phoenixrising-prod` |
| Web app | `app-phoenixrising-prod-<unique-suffix>` |
| Azure DevOps project | `PhoenixRising` |
| Azure DevOps service connection | `sc-phoenixrising-prod` |
| Azure DevOps environment | `phoenixrising-production` |

The web app name must be globally available. Copy the exact default hostname from its Azure Overview page; do not assume it is simply `<name>.azurewebsites.net`, because Azure can add a unique suffix/region.

## 2. Create the Azure Web App

In [Azure Portal](https://portal.azure.com), search for **App Services**, select **Create → Web App**, and choose:

- Your subscription and a new resource group named `rg-phoenixrising-prod`.
- A globally available web app name.
- Publish: **Code**.
- Runtime: **.NET 10**.
- Operating system: **Linux**.
- Region: a suitable location for Dawn's expected customers.
- A new Linux App Service plan named `asp-phoenixrising-prod`.
- Pricing: **Basic B1** is a reasonable starting tier for this small business site. Review the actual regional price before creating it. Higher tiers can be chosen later if needed.

Leave Deployment Center's GitHub continuous deployment setup unconfigured; Azure DevOps will own deployment. Review optional monitoring choices and the displayed costs, then create the app.

In the resulting app:

1. Copy the **Name** and **Default domain** from Overview.
2. Enable **HTTPS Only** under the app's configuration/general settings.
3. Enable **Always On** on the paid plan.
4. Verify the stack is .NET 10. The pipeline also applies `DOTNETCORE|10.0` and startup command `dotnet DawnOfThePhoenix.Web.dll`.

You can confirm current Linux runtimes in Azure Cloud Shell:

```sh
az webapp list-runtimes --os linux
```

The initial Azure placeholder page is expected before the first deployment. See Microsoft's [ASP.NET quickstart](https://learn.microsoft.com/azure/app-service/quickstart-dotnetcore) and [ASP.NET Core App Service configuration](https://learn.microsoft.com/azure/app-service/configure-language-dotnetcore).

## 3. Set application configuration

Open the web app's **Settings → Environment variables → App settings** (some portal layouts call this **Configuration → Application settings**). Add these values, then save/apply:

| Name | Initial value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Site__PublicBaseUrl` | `https://<the exact default hostname>` |
| `AllowedHosts` | `<the exact default hostname>` (no scheme or path) |
| `Site__Email` | `dawn@dawnofthephoenix.com` |

When Dawn's provider accounts are ready, add:

| Name | Value |
| --- | --- |
| `CalCom__Username` | Actual Cal.com username |
| `CalCom__ThirtyMinuteEvent` | Actual 30-minute event slug |
| `CalCom__SixtyMinuteEvent` | Actual 60-minute event slug |
| `Stripe__SecretKey` | A Stripe test secret initially; live secret after validating the setup |

Use two underscores exactly. These map to ASP.NET's nested configuration keys. Keep the Stripe key in the App Service settings or an Azure Key Vault reference; it does not belong in GitHub, pipeline YAML, or chat. The pipeline does not need the key to build, check, or deploy the application.

The website works with email fallbacks while the Cal.com/Stripe settings are absent. Live-session payments are configured within Cal.com's Stripe integration. Written readings and tips use the site's Stripe checkout handlers. Set Stripe's public business terms URL to `https://<your site hostname>/terms`, enable the desired receipts/notifications, and test question delivery and fulfillment before accepting payments.

The application includes App Service-specific forwarded-header handling before HTTPS redirection. It activates when Azure supplies `WEBSITE_INSTANCE_ID` and trusts Microsoft's documented private proxy ranges. Do not manually supply that platform variable for ordinary local development.

## 4. Create the Azure DevOps project

At [Azure DevOps](https://dev.azure.com), create or open your organization, then create a **private** project named `PhoenixRising`. Your GitHub repository remains the source; you do not need to import it into Azure Repos.

Under **Pipelines → Environments**, create an environment named exactly `phoenixrising-production`, with resource type **None**. This records deployment history. The supplied pipeline deploys automatically after successful `main` builds; no manual approval is configured by this YAML.

## 5. Connect Azure DevOps to Azure

In **Project settings → Service connections → New service connection**:

1. Choose **Azure Resource Manager**.
2. Use the automatic app-registration/identity option with **Workload identity federation**. The exact UI wording can vary.
3. Select the Azure subscription and scope it to `rg-phoenixrising-prod`.
4. Name the connection **`sc-phoenixrising-prod`**.
5. Authorize this specific deployment pipeline when prompted rather than granting access to every pipeline.

Federation lets Azure DevOps obtain deployment tokens without storing a long-lived deployment password. This Azure connection is separate from the GitHub connection used to read your repository. If the subscription is missing from the picker, check your selected Microsoft Entra directory and Azure account permissions.

See Microsoft's [Azure Resource Manager service connection guide](https://learn.microsoft.com/azure/devops/pipelines/library/connect-to-azure?view=azure-devops).

## 6. Create the pipeline from GitHub

In **Pipelines → New pipeline**:

1. Choose **GitHub** and authorize the **Azure Pipelines** GitHub app for `DavidEaton/DawnOfThePhoenix`.
2. Select that repository. This is a separate app authorization from the ChatGPT Codex Connector.
3. Choose **Existing Azure Pipelines YAML file**.
4. Choose `/azure-pipelines.yml` from the deployment feature branch while this PR is open, or `main` after it is merged.
5. Save the pipeline without changing the YAML file in `main` through the wizard.
6. In the pipeline's **Variables**, add two non-secret values:
   - `webAppName`: the exact App Service resource name from Overview.
   - `webAppHostName`: its exact default hostname, without `https://` or a trailing slash.
7. Authorize the service connection and deployment environment for this pipeline if prompted.

If you used another service-connection name, update `azureServiceConnection` in the YAML on a feature branch. The environment name in YAML must likewise match the environment created above.

Run the pipeline against the feature branch first. The Build stage should restore, build, run the executable C# checks, publish, and archive the app. **Deploy is skipped for feature branches and PRs.**

After reviewing and merging this PR, run against `main` or let the merge trigger CI. Set the pipeline's default branch for manual/scheduled runs to `main` if it was initially created from the feature branch. A successful `main` build proceeds to Deploy and publishes the saved artifact.

The checked-in pipeline does not use `dotnet test`, because the current checks are an executable C# project. It runs `dotnet run --project tests/DawnOfThePhoenix.Checks` instead. No real charges are made by the checks.

Microsoft documents this integration in [Build GitHub repositories with Azure Pipelines](https://learn.microsoft.com/azure/devops/pipelines/repos/github?view=azure-devops) and the deployment task in [AzureWebApp@1](https://learn.microsoft.com/azure/devops/pipelines/tasks/reference/azure-web-app-v1?view=azure-pipelines).

## 7. Verify the first deployment

Open `https://<the exact default hostname>`. Confirm that the PhoenixRising design, images, navigation, and terms/privacy pages load. Try the booking buttons and written-reading/tip dialogs. Until accounts are configured, they should explain the email fallback.

In Azure DevOps, check that both Build and Deploy are green. The final deployment step requests the website and checks for PhoenixRising content. The `webapp` artifact contains `website.zip`, with the DLL and `wwwroot` at the archive root.

If you want PR validation to be mandatory before merging, enable a GitHub branch rule/ruleset for `main` requiring a PR and the Azure Pipelines status check. Run the pipeline once so its real status-check name is available to select. In Azure DevOps, keep fork-build access to secrets/service connections disabled for this public repository.

Your ongoing workflow is: create a feature branch, push changes, open a PR, check the build, review, merge, and let Azure deploy `main`.

## 8. Attach your custom domain after the default hostname works

In App Service **Custom domains**, add the intended website hostname. Follow the portal's exact ownership-validation and DNS instructions, including the TXT and CNAME/A values it supplies. Keep existing email/MX records intact.

After validation, configure a managed TLS certificate/HTTPS binding where supported by your domain configuration. Update:

- `Site__PublicBaseUrl` to the custom HTTPS origin.
- `AllowedHosts` to include both the custom hostname and the default Azure hostname, separated by semicolons. Keep the default hostname because the pipeline uses it for its smoke check.
- Stripe's terms URL and any provider URLs that refer to the website.

The default Azure hostname remains useful for troubleshooting and pipeline verification.

## Troubleshooting and rollback

| Symptom | What to check |
| --- | --- |
| No hosted parallelism is available | Check **Organization settings → Pipelines → Parallel jobs**. New organizations may need a Microsoft-hosted parallelism grant or paid agent capacity. |
| Service connection/environment not found or unauthorized | Check the exact names, that the environment exists, and this pipeline's resource permissions. |
| Deployment receives 401/403 | Check the federated identity's access to the resource group/app and any App Service SCM access restrictions. |
| Azure placeholder remains | Verify the Deploy stage ran on `main`, the target app name is correct, and the ZIP contains the published app at its root. |
| Application fails to start | Open App Service **Log stream** and deployment logs. Check .NET 10 runtime, DLL startup command, production host settings, and the deployed artifact. |
| Redirect loop | Check HTTPS Only and ensure the deployed app includes the App Service proxy handling. Do not change the production environment to Development. |
| Homepage loads but payment buttons show an error | Check Stripe configuration, test/live mode, account activation, and the Stripe terms URL. The pipeline's smoke check does not create payments. |
| Booking iframe is unavailable | Use its external-page link and verify the configured Cal.com username/event slugs. |

For rollback, revert the offending change through a new GitHub PR and let `main` deploy the revert. Retained pipeline artifacts can also be redeployed deliberately when needed. Basic-tier deployment updates the live app directly and can briefly interrupt service; staging-slot swaps are a later option on a plan that supports them.

## What has and has not been verified

The pipeline is prepared in source. Its restore/build/check/publish commands can be run locally, and the Azure proxy behavior is covered by C# middleware checks. YAML structure and the ZIP contents can also be checked locally. Actual Azure DevOps execution, workload federation, App Service provisioning, domain ownership, and real provider transactions require your accounts and are not established by those local checks.
