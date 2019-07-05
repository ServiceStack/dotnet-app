dotnet build ..\example-plugins\ScriptInfo
copy ..\example-plugins\ScriptInfo\bin\Debug\netcoreapp2.1\ScriptInfo.dll ..\apps\plugins\plugins

dotnet build ..\example-plugins\ServerInfo
copy ..\example-plugins\ServerInfo\bin\Debug\netcoreapp2.1\ServerInfo.dll ..\apps\plugins\plugins

dotnet run ..\apps\plugins\app.settings
