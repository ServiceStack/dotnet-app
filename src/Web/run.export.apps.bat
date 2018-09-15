SET NETCORE_TEMPLATES=C:\src\NetCoreTemplates
SET WEB_APPS=C:\src\NetCoreWebApps

XCOPY /E /Y ..\apps\bare %NETCORE_TEMPLATES%\bare-webapp\
DEL %NETCORE_TEMPLATES%\bare-webapp\app\app.min.settings

XCOPY /E /Y ..\apps\rockwind %NETCORE_TEMPLATES%\rockwind-webapp\
DEL %NETCORE_TEMPLATES%\rockwind-webapp\app.*
COPY ..\apps\rockwind\app.template.settings %NETCORE_TEMPLATES%\rockwind-webapp\app.settings
COPY ..\apps\northwind.sqlite %NETCORE_TEMPLATES%\rockwind-webapp\

XCOPY /E /Y ..\apps\bare %WEB_APPS%\bare\

XCOPY /E /Y ..\apps\blog %WEB_APPS%\blog\
DEL %WEB_APPS%\blog\app.release.settings

XCOPY /E /Y ..\apps\chat %WEB_APPS%\chat\

XCOPY /E /Y ..\apps\plugins %WEB_APPS%\plugins\

XCOPY /E /Y ..\apps\redis %WEB_APPS%\redis\

XCOPY /E /Y ..\apps\redis-html %WEB_APPS%\redis-html\

XCOPY /E /Y ..\apps\rockwind %WEB_APPS%\rockwind\

COPY ..\apps\northwind.sqlite %WEB_APPS%\rockwind\
COPY ..\apps\northwind.sqlite %WEB_APPS%\rockwind-aws\
COPY ..\apps\northwind.sqlite %WEB_APPS%\rockwind-azure\

XCOPY /E /Y ..\apps\rockwind-vfs %WEB_APPS%\rockwind-aws\
DEL %WEB_APPS%\rockwind-aws\app.*.settings %WEB_APPS%\rockwind-aws\app.*.settings
COPY ..\apps\rockwind-vfs\app.aws.settings %WEB_APPS%\rockwind-aws\app.settings
COPY ..\apps\rockwind-vfs\app.aws.settings %WEB_APPS%\rockwind-aws\app.settings
MOVE %WEB_APPS%\rockwind-aws\template.app.sqlite.settings %WEB_APPS%\rockwind-aws\app.sqlite.settings

XCOPY /E /Y ..\apps\rockwind-vfs %WEB_APPS%\rockwind-azure\
DEL %WEB_APPS%\rockwind-azure\app.*.settings
COPY ..\apps\rockwind-vfs\app.azure.settings %WEB_APPS%\rockwind-azure\app.settings
MOVE %WEB_APPS%\rockwind-azure\template.app.sqlite.settings %WEB_APPS%\rockwind-azure\app.sqlite.settings
