#for /f "tokens=*" %%a in ('dir /a-d /o-n /b nupkg\*') do set NEWEST=%%a&& goto :next
#:next

set NEWEST=u.0.0.3.nupkg
dotnet nuget push nupkg/$NEWEST -k $NUGET_APIKEY -s https://www.nuget.org/api/v2/package