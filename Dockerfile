FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY /src/GistRun/GistRun.csproj ./

RUN dotnet nuget add source https://www.myget.org/F/servicestack -n myget.org
RUN dotnet restore
COPY /src/GistRun .

RUN dotnet publish -c Release -o /app/publish \
  --no-restore

COPY /src/GistRun/NuGet.Config /app/publish

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS final

COPY /src/GistRun/prefetch.sh ./
RUN chmod +x prefetch.sh
RUN ./prefetch.sh

RUN adduser --disabled-password \
  --home /app \
  --gecos '' deploy && chown -R deploy /app

USER deploy
WORKDIR /app

EXPOSE 5000
COPY --from=build /app/publish .
ENV DOTNET_NOLOGO 1
ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV INSPECT_VARS .gistrun/vars.json
ENV ASPNETCORE_URLS http://*:5000

# instruct Kestrel to expose API on port 5000
ENTRYPOINT ["dotnet", "GistRun.dll"]
