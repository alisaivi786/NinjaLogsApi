FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY NinjaLogs.sln ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY Directory.Packages.props ./
COPY src/NinjaLogs.Api/NinjaLogs.Api.csproj src/NinjaLogs.Api/
COPY src/NinjaLogs.Shared/NinjaLogs.Shared.csproj src/NinjaLogs.Shared/
COPY src/NinjaLogs.Infrastructure/NinjaLogs.Infrastructure.csproj src/NinjaLogs.Infrastructure/
COPY src/NinjaLogs.Modules.Logging/NinjaLogs.Modules.Logging.csproj src/NinjaLogs.Modules.Logging/
COPY src/NinjaLogs.Modules.Identity/NinjaLogs.Modules.Identity.csproj src/NinjaLogs.Modules.Identity/
COPY src/NinjaLogs.Modules.Authorization/NinjaLogs.Modules.Authorization.csproj src/NinjaLogs.Modules.Authorization/
COPY src/NinjaLogs.Modules.ApiKeys/NinjaLogs.Modules.ApiKeys.csproj src/NinjaLogs.Modules.ApiKeys/

RUN dotnet restore src/NinjaLogs.Api/NinjaLogs.Api.csproj

COPY . .
RUN dotnet publish src/NinjaLogs.Api/NinjaLogs.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "NinjaLogs.Api.dll"]
