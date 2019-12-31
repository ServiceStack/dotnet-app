dotnet pack X.csproj -c release -o nupkg
dotnet tool uninstall -g x
dotnet tool install --add-source .\nupkg -g x