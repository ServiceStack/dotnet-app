REM dotnet pack WebApp.csproj -c release -o nupkg
dotnet build -c release
pushd nupkg
nuget pack ../app.nuspec -Properties "packages=C:\Users\mythz\.nuget\packages"
popd