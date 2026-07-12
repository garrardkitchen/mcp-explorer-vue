using Garrard.Mcp.Explorer.Core.Domain.HttpApi;

namespace Garrard.Mcp.Explorer.Api.Dtos.HttpApis;

public sealed record SaveHttpApiRequest(
    string Name,
    string BaseUrl,
    string Method,
    string Path,
    HttpApiAuthenticationMode AuthenticationMode,
    List<HttpApiHeader>? Headers,
    List<HttpApiQueryParam>? QueryParams,
    string? BodyTemplate,
    string? GroupName,
    List<string>? Tags,
    string? Note,
    HttpApiAzureCredentialsOptions? AzureCredentials,
    HttpApiApiKeyOptions? ApiKeyOptions,
    HttpApiBearerOptions? BearerOptions
);

public sealed record SaveHttpApiCollectionRequest(
    string Name,
    string? Description,
    List<string> EndpointIds,
    string? GroupName
);

public sealed record ExportHttpApisRequest(
    IReadOnlyList<string> Ids,
    string Password,
    bool IncludeCertificates = false
);

public sealed record ImportHttpApisRequest(
    HttpApiExportPayloadDto Payload,
    string Password
);

public sealed record HttpApiExportPayloadDto(
    int    Version,
    string Salt,
    string Nonce,
    string Data
);

public sealed record BookmarkRequest(
    string? Label,
    Dictionary<string, string?>? Inputs
);

public sealed record SetFavouriteRequest(bool IsFavourite);

public sealed record InvokeHttpApiRequest(
    Dictionary<string, string?>? Inputs
);

public sealed record CompareHttpApiRequest(
    Dictionary<string, string?>? Inputs
);

public sealed record RunHttpApiCollectionRequest(
    Dictionary<string, string?>? Inputs
);
