SET NETCORE_TEMPLATES=C:\src\NetCoreTemplates
SET WEB_APPS=C:\src\sharp-apps

REM XCOPY /E /Y ..\apps\bare %NETCORE_TEMPLATES%\bare-webapp\
REM DEL %NETCORE_TEMPLATES%\bare-webapp\app\app.min.settings

REM XCOPY /E /Y ..\apps\rockwind %NETCORE_TEMPLATES%\rockwind-webapp\
REM DEL %NETCORE_TEMPLATES%\rockwind-webapp\app.*
REM COPY ..\apps\rockwind\app.template.settings %NETCORE_TEMPLATES%\rockwind-webapp\app.settings
REM COPY ..\apps\northwind.sqlite %NETCORE_TEMPLATES%\rockwind-webapp\

REM XCOPY /E /Y ..\apps\bare %WEB_APPS%\bare\

XCOPY /E /Y ..\apps\blog %WEB_APPS%\blog\
DEL %WEB_APPS%\blog\app.release.settings

XCOPY /E /Y ..\apps\chat %WEB_APPS%\chat\

XCOPY /E /Y ..\apps\plugins %WEB_APPS%\plugins\

XCOPY /E /Y ..\apps\redis %WEB_APPS%\redis\

REM XCOPY /E /Y ..\apps\redis-html %WEB_APPS%\redis-html\

REM XCOPY /E /Y ..\apps\rockwind %WEB_APPS%\rockwind\

REM COPY ..\apps\northwind.sqlite %WEB_APPS%\rockwind\

REM MD %WEB_APPS%\rockwind-aws\app %WEB_APPS%\rockwind-azure\app
REM COPY ..\apps\northwind.sqlite %WEB_APPS%\rockwind-aws\app\
REM COPY ..\apps\northwind.sqlite %WEB_APPS%\rockwind-azure\app\

REM XCOPY /E /Y ..\apps\rockwind-vfs %WEB_APPS%\rockwind-aws\app\
REM DEL %WEB_APPS%\rockwind-aws\app\app.*.settings %WEB_APPS%\rockwind-aws\app\app.*.settings
REM COPY ..\apps\rockwind-vfs\app.aws.settings %WEB_APPS%\rockwind-aws\app\app.settings
REM COPY ..\apps\rockwind-vfs\app.aws.settings %WEB_APPS%\rockwind-aws\app\app.settings
REM MOVE %WEB_APPS%\rockwind-aws\template.app.sqlite.settings %WEB_APPS%\rockwind-aws\app\app.sqlite.settings

REM XCOPY /E /Y ..\apps\rockwind-vfs %WEB_APPS%\rockwind-azure\app\
REM DEL %WEB_APPS%\rockwind-azure\app\app.*.settings
REM COPY ..\apps\rockwind-vfs\app.azure.settings %WEB_APPS%\rockwind-azure\app\app.settings
REM MOVE %WEB_APPS%\rockwind-azure\app\template.app.sqlite.settings %WEB_APPS%\rockwind-azure\app\app.sqlite.settings
