namespace Garrard.Mcp.Explorer.Core.Domain.DevTunnels;

public sealed class DevTunnelCliUnavailableException : InvalidOperationException
{
    public DevTunnelCliUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
