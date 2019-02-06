dotnet build ..\example-plugins\FilterInfo
copy ..\example-plugins\FilterInfo\bin\Debug\netcoreapp2.1\FilterInfo.dll ..\apps\plugins2\plugins

dotnet build ..\example-plugins\ServerInfo
copy ..\example-plugins\ServerInfo\bin\Debug\netcoreapp2.1\ServerInfo.dll ..\apps\plugins2\plugins

dotnet run ..\apps\plugins2\app.settings
