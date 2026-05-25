using System.Net;

namespace Garrard.Mcp.Explorer.Infrastructure;

/// <summary>
/// Rewrites loopback hosts to <c>host.docker.internal</c> when the app runs inside
/// a Docker container so requests still reach services running on the host machine.
/// </summary>
public static class ContainerLocalhostUriRewriter
{
    public static Uri Rewrite(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!IsRunningInContainer() || !IsHostLoopbackAccessEnabled())
        {
            return uri;
        }

        var host = uri.DnsSafeHost.Trim('[', ']');
        var isLocalhostName = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
        var isLoopbackAddress = IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

        if (!isLocalhostName && !isLoopbackAddress)
        {
            return uri;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        return new UriBuilder(uri)
        {
            Host = "host.docker.internal"
        }.Uri;
    }

    private static bool IsRunningInContainer()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable("RUNNING_IN_CONTAINER"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return File.Exists("/.dockerenv");
    }

    private static bool IsHostLoopbackAccessEnabled()
        => string.Equals(
            Environment.GetEnvironmentVariable("NETWORKING__AllowHostLoopbackAccess"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
