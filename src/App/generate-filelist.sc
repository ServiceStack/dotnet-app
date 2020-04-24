* use output in obj\Release\app.{version}.nuspec instead *

vfsFileSystem('.') |> to => fs

[] |> to => sb
fs.fileTextContents('obj/Release/netcoreapp3.1/WebApp.csproj.FileListAbsolute.txt').readLines() |> to => lines
#each line in lines where line.contains('\\bin\\')

    line.substring(line.indexOf('\\bin\\') + 1) |> to => src
    line.lastRightPart('\\') |> to => target

    `<file src="${src}" target="tools\\netcoreapp3.1\\any\\${target}" />`.raw()

/each
