# Use the official .NET SDK image for .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# Copy project files and restore dependencies
COPY src/ ./src/
WORKDIR /src/src/JobAggregator.Web

RUN dotnet restore

# Build and publish
RUN dotnet publish -c Release -o /app

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app .
COPY --from=build-env /src/src/JobAggregator.Web/appsettings.json .

ENTRYPOINT ["dotnet", "JobAggregator.Web.dll"]