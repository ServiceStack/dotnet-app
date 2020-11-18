* use output in obj\Release\app.{version}.nuspec instead *

var fs = vfsFileSystem('obj/Release')
var nuspec = fs.allRootFiles().last().fileContents()
var lines = nuspec.readLines()

* nuspec.raw() *

#each line in lines where line.trim().startsWith('<file ')

    line.replace('""','"').replace('C:\\src\\dotnet-app\\src\\App\\','').raw()

/each