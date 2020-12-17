mkdir prefetch
cd prefetch
dotnet nuget add source https://www.myget.org/F/servicestack -n myget.org
dotnet new console
dotnet add package ServiceStack.Server
dotnet add package ServiceStack.Aws
dotnet add package ServiceStack.Azure
dotnet add package ServiceStack.OrmLite.Sqlite
cd ..
rm -rf prefetch
