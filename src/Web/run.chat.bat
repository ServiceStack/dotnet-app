dotnet build ..\example-plugins\Chat
copy ..\example-plugins\Chat\bin\Debug\netcoreapp2.1\Chat.dll ..\apps\chat\plugins

dotnet run ..\apps\chat\app.settings
