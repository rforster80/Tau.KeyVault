FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Build stage

COPY ["Tau.KeyVault.csproj", "./"]
RUN dotnet restore "Tau.KeyVault.csproj" --configfile nuget.config

COPY . .
RUN dotnet publish "Tau.KeyVault.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app


ARG APP_VERSION=0.0.0.0
LABEL org.opencontainers.image.version=$APP_VERSION
ENV APP_VERSION=$APP_VERSION


COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080
ENTRYPOINT ["dotnet","Tau.KeyVault.dll"]
