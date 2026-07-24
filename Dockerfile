FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/FileProcessing.Api/FileProcessing.Api.csproj src/FileProcessing.Api/
RUN dotnet restore src/FileProcessing.Api/FileProcessing.Api.csproj

COPY src/FileProcessing.Api/ src/FileProcessing.Api/
RUN dotnet publish src/FileProcessing.Api/FileProcessing.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

USER app

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FileProcessing.Api.dll"]
