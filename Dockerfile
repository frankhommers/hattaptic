FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Install Logi Plugin Tool for packaging
RUN dotnet tool install --global LogiPluginTool
ENV PATH="$PATH:/root/.dotnet/tools"

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

# Package with logiplugintool
RUN logiplugintool pack ./bin/Release/ ./HaTTaPtic.lplug4 \
    && logiplugintool verify ./HaTTaPtic.lplug4 \
    && echo "Package verified"

# Output stage — build artifacts + .lplug4
FROM scratch AS output
COPY --from=build /src/bin/Release/ /output/
COPY --from=build /src/HaTTaPtic.lplug4 /HaTTaPtic.lplug4
