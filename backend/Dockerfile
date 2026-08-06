# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["ConfigSystem.Api/ConfigSystem.Api.csproj", "ConfigSystem.Api/"]
RUN dotnet restore "ConfigSystem.Api/ConfigSystem.Api.csproj"

# Copy source code
COPY ConfigSystem.Api/. ConfigSystem.Api/
WORKDIR "/src/ConfigSystem.Api"

# Build the application
RUN dotnet build "ConfigSystem.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ConfigSystem.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

# Expose port
EXPOSE 80

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80 \
    ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ConfigSystem.Api.dll"]
