FROM microsoft/dotnet:2.0-sdk
COPY src/apps/web /web
ADD https://raw.githubusercontent.com/NetCoreApps/WebApp/master/src/apps/rockwind-vfs/web.aws.settings /web/web.settings
WORKDIR /web
EXPOSE 5000/tcp
ENV ASPNETCORE_URLS https://*:5000
ENTRYPOINT ["dotnet", "/web/app.dll"]