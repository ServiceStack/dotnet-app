for /f "tokens=*" %%a in ('dir /a-d /o-n /b nupkg\*') do set NEWEST=%%a&& goto :next
:next

nuget push nupkg\%NEWEST% -Source https://www.nuget.org/api/v2/package