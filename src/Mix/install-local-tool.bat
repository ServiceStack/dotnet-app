dotnet pack Mix.csproj -c release -o nupkg
dotnet tool uninstall -g mix
dotnet tool install --add-source .\nupkg -g mix