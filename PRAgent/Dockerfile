# PRAgent Dockerfile
# Supports running PRAgent in a container with environment variable configuration

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PRAgent/PRAgent.csproj", "PRAgent/"]
RUN dotnet restore "./PRAgent/PRAgent.csproj"
COPY . .
WORKDIR "/src/PRAgent"
RUN dotnet build "./PRAgent.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "./PRAgent.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Install CA certificates for HTTPS calls
RUN apt-get update && apt-get install -y ca-certificates && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Copy default configuration (can be overridden by environment variables)
COPY PRAgent/appsettings.json ./appsettings.json

# Set entrypoint
ENTRYPOINT ["dotnet", "PRAgent.dll"]

# Default command shows help
CMD ["--help"]
