mkdir prefetch
cd prefetch
dotnet new console
dotnet add package ServiceStack.Server
dotnet add package ServiceStack.Aws
dotnet add package ServiceStack.Azure
dotnet add package ServiceStack.OrmLite.Sqlite
cd ..
rm -rf prefetch
