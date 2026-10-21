using DawnOfThePhoenix.Web.Options;
using DawnOfThePhoenix.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.Configure<CalComOptions>(builder.Configuration.GetSection("CalCom"));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
builder.Services.AddHttpClient<StripeCheckoutService>(client =>
{
    client.BaseAddress = new Uri("https://api.stripe.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Only enable App Service proxy trust when the platform identifies this process.
var isAppService = !string.IsNullOrWhiteSpace(builder.Configuration["WEBSITE_INSTANCE_ID"]);
if (isAppService)
{
    builder.Services.Configure<ForwardedHeadersOptions>(AzureAppServiceProxy.Configure);
}

var app = builder.Build();
if (isAppService)
{
    // Must run before HTTPS redirection, HSTS, and antiforgery cookie generation.
    app.UseForwardedHeaders();
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next(context);
});
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();

public partial class Program { }
