namespace DawnOfThePhoenix.Web.Options;

public sealed class CalComOptions
{
    public string Username { get; set; } = "";
    public string ThirtyMinuteEvent { get; set; } = "";
    public string SixtyMinuteEvent { get; set; } = "";
}

public sealed class StripeOptions
{
    public string SecretKey { get; set; } = "";
}

public sealed class SiteOptions
{
    public string Email { get; set; } = "dawn@dawnofthephoenix.com";
    public string PublicBaseUrl { get; set; } = "";
}
