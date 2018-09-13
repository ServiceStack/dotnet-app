# Web App Releases

Releases for ServiceStack Web Apps.

## Install

If you have Linux, OSX or [Windows Subsystem for Linux](https://msdn.microsoft.com/en-us/commandline/wsl/install_guide), 
from the directory where all your Web Apps are, run:

     curl -L https://github.com/NetCoreWebApps/Web/archive/v0.1.tar.gz | tar xz && mv Web-0.1 web

Otherwise for Windows without access to linux tools, download:

 - [Web v0.1.zip](https://github.com/NetCoreWebApps/Web/archive/v0.1.zip)

Then copy all contents into the `web` folder next to where all your Web Apps are.

## Usage

To run install [.NET Core 2.1 for your platform](https://www.microsoft.com/net/download/core) then for Linux, OSX and Windows, run:

    dotnet web/app.dll ../<app name>/web.settings

Replacing `<app name>` with the app you wish to run.

## Web App Examples

See [templates.servicestack.net/docs/web-apps](http://templates.servicestack.net/docs/web-apps) for examples of different Web Apps and 
`web.settings` configuration.