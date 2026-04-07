dataRoot="$HOME/Library/Application Support/McpExplorerv2"
docker build -t mcp-explorer-x . && docker run --rm -it -p 8091:8080 \
  -v "${dataRoot}:/data" \
  -v ~/.azure:/root/.azure:ro \
  -e AZURE_CONFIG_DIR=/root/.azure \
  -e HOST_AZURE_CONFIG_DIR=${HOME}/.azure \
  -e PREFERENCES__StoragePath=/data/settings.json \
  -e ASPNETCORE_ENVIRONMENT=Production \
  mcp-explorer-x