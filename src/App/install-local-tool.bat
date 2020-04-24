CALL pack.bat
REM dotnet pack WebApp.csproj -c release -o nupkg
dotnet tool uninstall -g app
dotnet tool install --add-source .\nupkg -g app