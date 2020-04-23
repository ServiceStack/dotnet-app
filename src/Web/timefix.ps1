gci -path "C:\Users\mythz\.nuget\packages" -rec -file *.dll | Where-Object {$_.LastWriteTime -lt (Get-Date).AddYears(-20)} | %  { try { $_.LastWriteTime = '01/01/2020 00:00:00' } catch {} }
