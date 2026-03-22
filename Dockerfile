FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy runtime dependencies from lib/ (synced from Logi Options+)
COPY lib/*.dll /opt/PluginApi/

# Copy project file and restore
COPY src/HaTTaPticPlugin.csproj ./src/
RUN dotnet restore src/HaTTaPticPlugin.csproj -p:PluginApiDir=/opt/PluginApi/

# Copy source code
COPY src/ ./src/

# Build
RUN dotnet build src/HaTTaPticPlugin.csproj -c Release -p:PluginApiDir=/opt/PluginApi/ -p:SkipPostBuild=true --no-restore \
    && echo "Build successful"

# Output stage — just the build artifacts
FROM scratch AS output
COPY --from=build /src/bin/Release/ /output/
