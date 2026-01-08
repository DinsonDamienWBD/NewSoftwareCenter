# ==================== Stage 1: Build ====================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["NewSoftwareCenter.slnx", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Host/Host.csproj", "Host/"]
COPY ["Kernel/Core/Core.csproj", "Kernel/Core/"]
COPY ["Kernel/DataWarehouse/DataWarehouse.Kernel/DataWarehouse.Kernel.csproj", "Kernel/DataWarehouse/DataWarehouse.Kernel/"]
COPY ["Kernel/DataWarehouse/DataWarehouse.SDK/DataWarehouse.SDK.csproj", "Kernel/DataWarehouse/DataWarehouse.SDK/"]
COPY ["Kernel/DataWarehouse/DataWarehouse.CLI/DataWarehouse.CLI.csproj", "Kernel/DataWarehouse/DataWarehouse.CLI/"]

# Copy all plugin project files
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Compression.Standard/DataWarehouse.Plugins.Compression.Standard.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Compression.Standard/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Crypto.Standard/DataWarehouse.Plugins.Crypto.Standard.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Crypto.Standard/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Feature.Tiering/DataWarehouse.Plugins.Feature.Tiering.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Feature.Tiering/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Intelligence.Governance/DataWarehouse.Plugins.Intelligence.Governance.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Intelligence.Governance/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Interface.gRPC/DataWarehouse.Plugins.Interface.gRPC.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Interface.gRPC/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Interface.REST/DataWarehouse.Plugins.Interface.REST.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Interface.REST/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Interface.SQL/DataWarehouse.Plugins.Interface.SQL.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Interface.SQL/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Metadata.Postgres/DataWarehouse.Plugins.Metadata.Postgres.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Metadata.Postgres/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Metadata.SQLite/DataWarehouse.Plugins.Metadata.SQLite.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Metadata.SQLite/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Orchestration.Raft/DataWarehouse.Plugins.Orchestration.Raft.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Orchestration.Raft/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Security.ACL/DataWarehouse.Plugins.Security.ACL.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Security.ACL/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Storage.IpfsNew/DataWarehouse.Plugins.Storage.IpfsNew.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Storage.IpfsNew/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Storage.LocalNew/DataWarehouse.Plugins.Storage.LocalNew.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Storage.LocalNew/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Storage.RAMDisk/DataWarehouse.Plugins.Storage.RAMDisk.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Storage.RAMDisk/"]
COPY ["Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Storage.S3New/DataWarehouse.Plugins.Storage.S3New.csproj", "Kernel/DataWarehouse/Plugins/DataWarehouse.Plugins.Storage.S3New/"]

# Copy manager project files
COPY ["Kernel/Managers/BackendManager/Manager.csproj", "Kernel/Managers/BackendManager/"]
COPY ["Kernel/Managers/FrontendManager/FrontendManager.csproj", "Kernel/Managers/FrontendManager/"]

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY . .

# Build the application
WORKDIR /src/Host
RUN dotnet build "Host.csproj" -c Release -o /app/build

# ==================== Stage 2: Publish ====================
FROM build AS publish
RUN dotnet publish "Host.csproj" -c Release -o /app/publish \
    --no-restore \
    /p:PublishSingleFile=false \
    /p:PublishTrimmed=false

# ==================== Stage 3: Runtime ====================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install runtime dependencies
RUN apt-get update && apt-get install -y \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r datawarehouse && useradd -r -g datawarehouse datawarehouse

# Create necessary directories
RUN mkdir -p /app/data /app/logs /app/plugins /app/config && \
    chown -R datawarehouse:datawarehouse /app

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R datawarehouse:datawarehouse /app

# Switch to non-root user
USER datawarehouse

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DW_ROOT_PATH=/app/data \
    DW_LOG_PATH=/app/logs \
    DW_PLUGIN_PATH=/app/plugins

# Expose ports
# 5000 - HTTP
# 5001 - HTTPS
# 50051 - gRPC
# 5432 - SQL Interface
EXPOSE 5000 5001 50051 5432

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Volume mounts for persistent data
VOLUME ["/app/data", "/app/logs", "/app/config"]

# Entry point
ENTRYPOINT ["dotnet", "Host.dll"]

# Default arguments (can be overridden)
CMD ["--mode", "standalone"]

# ==================== Metadata Labels ====================
LABEL maintainer="DataWarehouse Team" \
      version="1.0.0" \
      description="AI-Native DataWarehouse with plugin architecture" \
      org.opencontainers.image.source="https://github.com/DinsonDamienWBD/NewSoftwareCenter" \
      org.opencontainers.image.title="DataWarehouse" \
      org.opencontainers.image.description="Production-ready AI-Native data storage platform" \
      org.opencontainers.image.vendor="DataWarehouse Project" \
      org.opencontainers.image.licenses="MIT"
