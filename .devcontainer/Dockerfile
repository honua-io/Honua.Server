# Use Ubuntu with .NET 9 and GDAL support
FROM mcr.microsoft.com/devcontainers/dotnet:1-9.0-bookworm

# Switch to root for system packages
USER root

# Install GDAL, PROJ, and spatial dependencies
RUN apt-get update && apt-get install -y \
    gdal-bin \
    libgdal-dev \
    proj-bin \
    proj-data \
    libproj-dev \
    sqlite3 \
    libsqlite3-dev \
    postgresql-client \
    mysql-client \
    redis-tools \
    curl \
    wget \
    git \
    && rm -rf /var/lib/apt/lists/*

# Verify GDAL installation
RUN gdalinfo --version && \
    proj && \
    echo "GDAL_DATA=$(gdal-config --datadir)" && \
    echo "PROJ_LIB=$(proj --info | grep 'Database path:' | cut -d: -f2 | xargs dirname)"

# Set environment variables for GDAL and PROJ
ENV GDAL_DATA=/usr/share/gdal
ENV PROJ_LIB=/usr/share/proj

# Create gdal-data directory for mounting
RUN mkdir -p /usr/share/gdal && \
    cp -r $(gdal-config --datadir)/* /usr/share/gdal/ || true

# Switch back to vscode user
USER vscode

# Set working directory
WORKDIR /workspaces/HonuaCore

# Restore global tools
RUN dotnet tool install --global dotnet-ef || true

# Add dotnet tools to PATH
ENV PATH="${PATH}:/home/vscode/.dotnet/tools"