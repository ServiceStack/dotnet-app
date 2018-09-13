XCOPY /E /Y ..\apps\bare ..\..\..\..\NetCoreTemplates\bare-webapp\
DEL ..\..\..\..\NetCoreTemplates\bare-webapp\app\app.min.settings

XCOPY /E /Y ..\apps\rockwind ..\..\..\..\NetCoreTemplates\rockwind-webapp\
DEL ..\..\..\..\NetCoreTemplates\rockwind-webapp\app.*
COPY ..\apps\rockwind\app.template.settings ..\..\..\..\NetCoreTemplates\rockwind-webapp\app.settings
COPY ..\apps\northwind.sqlite ..\..\..\..\NetCoreTemplates\rockwind-webapp\

RMDIR ..\..\..\WebAppStarter\app /s /q
XCOPY /E /Y ..\apps\bare ..\..\..\WebAppStarter\app\
RMDIR ..\..\..\WebAppStarter\web /s /q
XCOPY /E /Y ..\apps\web ..\..\..\WebAppStarter\web\

XCOPY /E /Y ..\apps\bare ..\..\..\bare\

XCOPY /E /Y ..\apps\blog ..\..\..\blog\
DEL ..\..\..\blog\app.release.settings

XCOPY /E /Y ..\apps\chat ..\..\..\chat\

XCOPY /E /Y ..\apps\plugins ..\..\..\plugins\

XCOPY /E /Y ..\apps\redis ..\..\..\redis\

XCOPY /E /Y ..\apps\redis-html ..\..\..\redis-html\

XCOPY /E /Y ..\apps\rockwind ..\..\..\rockwind\

COPY ..\apps\northwind.sqlite ..\..\..\rockwind\
COPY ..\apps\northwind.sqlite ..\..\..\rockwind-aws\
COPY ..\apps\northwind.sqlite ..\..\..\rockwind-azure\

XCOPY /E /Y ..\apps\rockwind-vfs ..\..\..\rockwind-aws\
DEL ..\..\..\rockwind-aws\app.*.settings ..\..\..\rockwind-aws\app.*.settings
COPY ..\apps\rockwind-vfs\app.aws.settings ..\..\..\rockwind-aws\app.settings
COPY ..\apps\rockwind-vfs\app.aws.settings ..\..\..\rockwind-aws\app.settings
MOVE ..\..\..\rockwind-aws\template.app.sqlite.settings ..\..\..\rockwind-aws\app.sqlite.settings

XCOPY /E /Y ..\apps\rockwind-vfs ..\..\..\rockwind-azure\
DEL ..\..\..\rockwind-azure\app.*.settings
COPY ..\apps\rockwind-vfs\app.azure.settings ..\..\..\rockwind-azure\app.settings
MOVE ..\..\..\rockwind-azure\template.app.sqlite.settings ..\..\..\rockwind-azure\app.sqlite.settings
