using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.HttpApi;

namespace Garrard.Mcp.Explorer.Cli.Commands;

/// <summary>
/// Builds request/response fields on an <see cref="HttpApiInvocationRecord"/> from the
/// resolved definition and invoker result, matching what the web app controller stores.
/// </summary>
internal static class CliInvocationHelper
{
    internal static void ApplyRequestResponse(
        HttpApiInvocationRecord record,
        HttpApiDefinition def,
        HttpApiInvokeResult result)
    {
        var resolved = HttpApiTemplateResolver.Apply(def, null);

        record.RequestMethod      = resolved.Method;
        record.RequestBaseUrl     = resolved.BaseUrl;
        record.RequestPath        = resolved.Path;
        record.RequestHeaders     = resolved.Headers
            .ToDictionary(h => h.Name, h => h.Value, StringComparer.OrdinalIgnoreCase);
        record.RequestQueryParams = resolved.QueryParams
            .Where(q => q.Enabled)
            .ToDictionary(q => q.Name, q => q.Value, StringComparer.OrdinalIgnoreCase);
        record.ResponseHeaders    = result.ResponseHeaders;
        record.ContentType        = result.ContentType;
        record.Body               = result.TruncatedBody;
    }
}
