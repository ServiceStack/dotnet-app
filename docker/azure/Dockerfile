FROM microsoft/dotnet:2.0-sdk
COPY src/apps/web /web
COPY src/apps/rockwind-vfs/web.azure.settings /web/web.settings
WORKDIR /web
EXPOSE 5000/tcp
ENV ASPNETCORE_URLS https://*:5000
ENTRYPOINT ["dotnet", "/web/app.dll"]