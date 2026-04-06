namespace Garrard.Mcp.Explorer.Api.Dtos.Connections;

public sealed record ExportConnectionsRequest(
    IReadOnlyList<string> Names,
    string Password
);

public sealed record ImportConnectionsRequest(
    ExportPayloadDto Payload,
    string Password
);

public sealed record ExportPayloadDto(
    int    Version,
    string Salt,
    string Nonce,
    string Data
);
