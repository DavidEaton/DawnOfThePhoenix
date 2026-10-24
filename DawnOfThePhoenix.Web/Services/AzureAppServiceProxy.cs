using Microsoft.AspNetCore.HttpOverrides;

namespace DawnOfThePhoenix.Web.Services;

public static class AzureAppServiceProxy
{
    // App Service terminates TLS at its load balancer. These are the private
    // proxy ranges documented by Microsoft, including IPv4-mapped IPv6 forms.
    // Keep public client addresses outside the trusted proxy list.
    public static void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        foreach (var network in new[]
        {
            "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",
            "::ffff:10.0.0.0/104", "::ffff:172.16.0.0/108", "::ffff:192.168.0.0/112"
        })
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
    }
}
