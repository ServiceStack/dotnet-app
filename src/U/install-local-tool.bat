dotnet pack U.csproj -c release -o nupkg
dotnet tool uninstall -g u
dotnet tool install --add-source .\nupkg -g u