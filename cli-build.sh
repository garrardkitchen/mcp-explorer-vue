#!/bin/bash

dotnet tool uninstall -g Garrard.Mcp.Explorer.Cli
dotnet pack src/Garrard.Mcp.Explorer.Cli -c Release -o ./nupkg
dotnet tool install -g Garrard.Mcp.Explorer.Cli --add-source ./nupkg