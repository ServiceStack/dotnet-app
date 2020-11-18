* use output in obj\Release\app.{version}.nuspec instead *

var fs = vfsFileSystem('.')

var sb = []
var lines = fs.fileTextContents('obj/Release/net5/App.csproj.FileListAbsolute.txt').readLines()
#each line in lines where line.contains('\\bin\\')

    var src = line.substring(line.indexOf('\\bin\\') + 1)
    var target = line.lastRightPart('\\')

    `<file src="${src}" target="tools\\net5\\any\\${target}" />`.raw()

/each
