# Optional application image; compose.yaml runs telemetry services only.
# Build from the solution root: docker build -f MVC_App/Dockerfile -t mvc-app:local .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY MVC_App/MVC_App.csproj MVC_App/
RUN dotnet restore MVC_App/MVC_App.csproj
COPY MVC_App/ MVC_App/
RUN dotnet publish MVC_App/MVC_App.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "MVC_App.dll"]
