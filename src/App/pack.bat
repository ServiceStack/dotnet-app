REM dotnet pack WebApp.csproj -c release -o nupkg
dotnet pack -c Release
pushd nupkg
nuget pack ../app.nuspec -Properties "cefdir=C:\src\ServiceStack\ServiceStack.CefGlue\src\ServiceStack.CefGlue.Win64"
popd