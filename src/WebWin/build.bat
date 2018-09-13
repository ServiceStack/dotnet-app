dotnet publish -c release -r win-x64

SET WEBWIN=..\..\..\WebWin

RMDIR %WEBWIN%\locales /s /q
RMDIR %WEBWIN%\swiftshader /s /q
DEL %WEBWIN%\*.dll %WEBWIN%\*.config %WEBWIN%\*.json %WEBWIN%\*.pak %WEBWIN%\*.exe %WEBWIN%\*.lib %WEBWIN%\*.bin %WEBWIN%\*.dat %WEBWIN%\*.ico
XCOPY /E bin\release\netcoreapp2.1\win-x64\publish %WEBWIN%\

DEL %WEBWIN%\*.pdb %WEBWIN%\*.xml
