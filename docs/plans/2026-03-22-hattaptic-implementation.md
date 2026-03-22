# HaTTaPtic Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Logi Options+ plugin that exposes MX Master 4 haptic feedback via HTTP at `http://127.72.80.84:8080/haptic/{waveform}`

**Architecture:** A C#/.NET 8 Logi Options+ plugin using the Loupedeck Plugin SDK. An embedded `HttpListener` handles incoming GET requests and triggers haptic waveforms via `Plugin.PluginEvents.RaiseEvent()`. Docker-based build to avoid local .NET SDK requirement.

**Tech Stack:** C# / .NET 8, Loupedeck Plugin SDK (PluginApi.dll), HttpListener, Docker

**Design doc:** `docs/plans/2026-03-22-hattaptic-design.md`

---

### Task 1: Project scaffolding — .gitignore and solution file

**Files:**
- Create: `.gitignore`
- Create: `HaTTaPtic.sln`

**Step 1: Create .gitignore**

```gitignore
# Build outputs
bin/
obj/
out/

# IDE
.vs/
.vscode/
*.suo
*.user
*.userosscache
*.sln.docstates
.idea/
*.swp
*~

# macOS
.DS_Store
._*

# .NET
*.dll
*.exe
*.pdb
*.cache
*.nupkg
project.lock.json

# NuGet
packages/

# Build results
[Dd]ebug/
[Rr]elease/
[Bb]in/
[Oo]bj/
[Ll]og/
[Ll]ogs/

# Plugin packages (generated)
*.lplug4

# Logs
*.log

# Temporary
tmp/
temp/
*.tmp
```

Write this to `.gitignore`.

**Step 2: Create solution file**

```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HaTTaPticPlugin", "src\HaTTaPticPlugin.csproj", "{B7C8D9E0-F1A2-3456-789A-BCDEF0123456}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{B7C8D9E0-F1A2-3456-789A-BCDEF0123456}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B7C8D9E0-F1A2-3456-789A-BCDEF0123456}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B7C8D9E0-F1A2-3456-789A-BCDEF0123456}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B7C8D9E0-F1A2-3456-789A-BCDEF0123456}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
```

Write this to `HaTTaPtic.sln`.

**Step 3: Commit**

```bash
git add .gitignore HaTTaPtic.sln
git commit -m "chore: add solution file and gitignore"
```

---

### Task 2: Project file and package metadata

**Files:**
- Create: `src/HaTTaPticPlugin.csproj`
- Create: `src/package/metadata/LoupedeckPackage.yaml`

**Step 1: Create the .csproj file**

This references `PluginApi.dll` from Logi Options+ (same approach as ReaperHaptic).
No external NuGet packages needed — we use the built-in `HttpListener` instead of OSC.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <RootNamespace>Loupedeck.HaTTaPticPlugin</RootNamespace>

    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>

    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>

    <PluginApiDir Condition="$(OS) == 'Windows_NT'">C:\Program Files\Logi\LogiPluginService\</PluginApiDir>
    <PluginApiDir Condition="$(OS) != 'Windows_NT'">/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/</PluginApiDir>

    <PluginDir Condition="$(OS) == 'Windows_NT'">$(LocalAppData)\Logi\LogiPluginService\Plugins\</PluginDir>
    <PluginDir Condition="$(OS) != 'Windows_NT'">~/Library/Application\ Support/Logi/LogiPluginService/Plugins/</PluginDir>

    <BaseOutputPath>$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..\bin\'))</BaseOutputPath>
    <OutputPath>$(BaseOutputPath)$(Configuration)\bin\</OutputPath>

    <PluginShortName>HaTTaPtic</PluginShortName>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="PluginApi">
      <HintPath>$(PluginApiDir)PluginApi.dll</HintPath>
    </Reference>
  </ItemGroup>

  <Target Name="CopyPackage" AfterTargets="PostBuildEvent">
    <Message Text="Copying package files..." Importance="High" />
    <ItemGroup>
      <PackageFiles Include="package\**\*" />
    </ItemGroup>
    <Copy SourceFiles="@(PackageFiles)" DestinationFolder="$(OutputPath)..\%(RecursiveDir)" />
    <Message Text="Package files copied to $(OutputPath)..\" Importance="High" />
  </Target>

  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Condition="$(OS) == 'Windows_NT'" Command="echo $(BaseOutputPath)$(Configuration)\ &gt; &quot;$(PluginDir)$(ProjectName).link&quot;" />
    <Exec Condition="$(OS) != 'Windows_NT'" Command="echo $(BaseOutputPath)$(Configuration)\ &gt; $(PluginDir)$(ProjectName).link" />
    <Message Text="Created plugin link at: $(PluginDir)$(ProjectName).link" Importance="High" />

    <Message Text="Sending plugin reload command for $(PluginShortName) to Logi Plugin Service" Importance="High" />
    <Exec Condition="$(OS) == 'Windows_NT'" Command="start loupedeck:plugin/$(PluginShortName)/reload" ContinueOnError="true" />
    <Exec Condition="$(OS) != 'Windows_NT'" Command="open loupedeck:plugin/$(PluginShortName)/reload" ContinueOnError="true" />
    <Message Condition="'$(MSBuildLastTaskResult)' == 'false'" Text="Couldn't reload the plugin, please restart the Logi Plugin Service manually" Importance="High" />
  </Target>

  <Target Name="PluginClean" AfterTargets="CoreClean">
    <Message Text="Cleaning up plugin link file and output directories..." Importance="High" />
    <Delete Condition="$(OS) == 'Windows_NT'" Files="$(PluginDir)$(ProjectName).link" />
    <Exec Condition="$(OS) != 'Windows_NT'" Command="rm -f $(PluginDir)$(ProjectName).link" />
    <RemoveDir Directories="$(OutputPath)..\" />
    <Message Text="Plugin link file and output directories have been cleaned!" Importance="High" />
  </Target>

</Project>
```

Write this to `src/HaTTaPticPlugin.csproj`.

**Step 2: Create LoupedeckPackage.yaml**

```yaml
type: plugin4
name: HaTTaPtic
displayName: HaTTaPtic
description: HTTP-triggered haptic feedback for Logitech MX Master 4. Send a GET request, the mouse vibrates.
pluginFileName: HaTTaPticPlugin.dll
version: 1.0
author: HaTTaPtic

pluginFolderMac: bin

supportedDevices: []

pluginCapabilities:
    - HasHapticMapping

minimumLoupedeckVersion: 6.0

license: MIT
licenseUrl: https://opensource.org/licenses/MIT
```

Write this to `src/package/metadata/LoupedeckPackage.yaml`.

**Step 3: Commit**

```bash
git add src/HaTTaPticPlugin.csproj src/package/metadata/LoupedeckPackage.yaml
git commit -m "chore: add project file and plugin package metadata"
```

---

### Task 3: Haptic event YAML configuration

**Files:**
- Create: `src/package/events/DefaultEventSource.yaml`
- Create: `src/package/events/extra/eventMapping.yaml`

**Step 1: Create DefaultEventSource.yaml**

This registers all waveform names as plugin events that Logi Options+ can map to haptic motor patterns.

```yaml
displayName: Haptic Events
description: HTTP-triggered haptic events for MX Master 4

events:
  - name: sharp_collision
    displayName: Sharp Collision
    description: Sharp, precise collision feedback

  - name: damp_collision
    displayName: Damp Collision
    description: Dampened collision feedback

  - name: subtle_collision
    displayName: Subtle Collision
    description: Subtle, light collision feedback

  - name: sharp_state_change
    displayName: Sharp State Change
    description: Sharp state change confirmation

  - name: damp_state_change
    displayName: Damp State Change
    description: Dampened state change confirmation

  - name: completed
    displayName: Completed
    description: Task completion feedback

  - name: angry_alert
    displayName: Angry Alert
    description: Strong warning/alert feedback

  - name: happy_alert
    displayName: Happy Alert
    description: Positive notification feedback

  - name: knock
    displayName: Knock
    description: Simple knock feedback

  - name: ringing
    displayName: Ringing
    description: Ringing notification feedback
```

Write this to `src/package/events/DefaultEventSource.yaml`.

**Step 2: Create eventMapping.yaml**

Maps each event name to its MX Master 4 waveform. The event name IS the waveform name, so this is a 1:1 mapping.

```yaml
haptics:
  sharp_collision:
    DEFAULT: knock
    MX Master 4: sharp_collision

  damp_collision:
    DEFAULT: knock
    MX Master 4: damp_collision

  subtle_collision:
    DEFAULT: knock
    MX Master 4: subtle_collision

  sharp_state_change:
    DEFAULT: knock
    MX Master 4: sharp_state_change

  damp_state_change:
    DEFAULT: knock
    MX Master 4: damp_state_change

  completed:
    DEFAULT: knock
    MX Master 4: completed

  angry_alert:
    DEFAULT: knock
    MX Master 4: angry_alert

  happy_alert:
    DEFAULT: knock
    MX Master 4: happy_alert

  knock:
    DEFAULT: knock
    MX Master 4: knock

  ringing:
    DEFAULT: knock
    MX Master 4: ringing
```

Write this to `src/package/events/extra/eventMapping.yaml`.

**Step 3: Commit**

```bash
git add src/package/events/
git commit -m "chore: add haptic event YAML configuration"
```

---

### Task 4: HapticEventRegistry — event registration and triggering

**Files:**
- Create: `src/HapticEventRegistry.cs`

**Step 1: Write HapticEventRegistry.cs**

Simple class: registers all waveform names as plugin events, provides a `Trigger()` method, and exposes the list of known waveforms.

```csharp
namespace Loupedeck.HaTTaPticPlugin
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Registers haptic waveforms as plugin events and provides triggering.
    /// </summary>
    public class HapticEventRegistry
    {
        private readonly Plugin _plugin;
        private readonly HashSet<string> _knownWaveforms;

        public static readonly string[] Waveforms = new[]
        {
            "sharp_collision",
            "damp_collision",
            "subtle_collision",
            "sharp_state_change",
            "damp_state_change",
            "completed",
            "angry_alert",
            "happy_alert",
            "knock",
            "ringing"
        };

        public HapticEventRegistry(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _knownWaveforms = new HashSet<string>(Waveforms, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers all waveforms as plugin events.
        /// </summary>
        public void RegisterAll()
        {
            foreach (var waveform in Waveforms)
            {
                _plugin.PluginEvents.AddEvent(waveform, waveform, $"Haptic waveform: {waveform}");
                PluginLog.Verbose($"Registered haptic event: {waveform}");
            }

            PluginLog.Info($"Registered {Waveforms.Length} haptic events");
        }

        /// <summary>
        /// Triggers a haptic waveform by name.
        /// Returns true if the waveform was found and triggered.
        /// </summary>
        public bool Trigger(string waveformName)
        {
            if (string.IsNullOrEmpty(waveformName))
            {
                return false;
            }

            if (!_knownWaveforms.Contains(waveformName))
            {
                PluginLog.Info($"Unknown waveform requested: {waveformName}");
                return false;
            }

            _plugin.PluginEvents.RaiseEvent(waveformName);
            PluginLog.Info($"Haptic triggered: {waveformName}");
            return true;
        }

        /// <summary>
        /// Returns true if the waveform name is known.
        /// </summary>
        public bool IsKnown(string waveformName) =>
            !string.IsNullOrEmpty(waveformName) && _knownWaveforms.Contains(waveformName);
    }
}
```

Write this to `src/HapticEventRegistry.cs`.

**Step 2: Commit**

```bash
git add src/HapticEventRegistry.cs
git commit -m "feat: add HapticEventRegistry for waveform registration and triggering"
```

---

### Task 5: HttpHapticServer — the HTTP listener

**Files:**
- Create: `src/HttpHapticServer.cs`

**Step 1: Write HttpHapticServer.cs**

Listens on `http://127.72.80.84:8080/`. Routes:
- `GET /haptic/{waveform}` — trigger haptic
- `GET /waveforms` — list available waveforms
- `GET /health` — health check
- Everything else — 404

```csharp
namespace Loupedeck.HaTTaPticPlugin
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP server that listens on 127.72.80.84:8080 and triggers haptic events.
    /// </summary>
    public class HttpHapticServer : IDisposable
    {
        private const string ListenAddress = "http://127.72.80.84:8080/";

        private readonly HapticEventRegistry _registry;
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;

        public HttpHapticServer(HapticEventRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(ListenAddress);
                _listener.Start();

                _cts = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoop(_cts.Token));

                PluginLog.Info($"HTTP server started on {ListenAddress}");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to start HTTP server on {ListenAddress}");
                throw;
            }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    // Fire and forget — don't block the listen loop
                    _ = Task.Run(() => HandleRequest(context), token);
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        PluginLog.Error(ex, "Error accepting HTTP request");
                    }
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var path = request.Url?.AbsolutePath?.TrimEnd('/') ?? "";

                PluginLog.Verbose($"HTTP {request.HttpMethod} {path}");

                if (request.HttpMethod != "GET")
                {
                    SendJson(response, 405, "{\"error\":\"Method not allowed. Use GET.\"}");
                    return;
                }

                if (path == "/health")
                {
                    SendJson(response, 200, "{\"status\":\"ok\"}");
                    return;
                }

                if (path == "/waveforms")
                {
                    var waveformsJson = BuildWaveformsJson();
                    SendJson(response, 200, waveformsJson);
                    return;
                }

                if (path.StartsWith("/haptic/"))
                {
                    var waveform = path.Substring("/haptic/".Length);

                    if (string.IsNullOrEmpty(waveform))
                    {
                        SendJson(response, 400, "{\"error\":\"Missing waveform name. Use /haptic/{waveform}\"}");
                        return;
                    }

                    if (_registry.Trigger(waveform))
                    {
                        SendJson(response, 200,
                            $"{{\"status\":\"ok\",\"waveform\":\"{EscapeJson(waveform)}\"}}");
                    }
                    else
                    {
                        SendJson(response, 404,
                            $"{{\"error\":\"Unknown waveform\",\"requested\":\"{EscapeJson(waveform)}\",\"available\":{BuildWaveformArray()}}}");
                    }
                    return;
                }

                // Unknown route
                SendJson(response, 404,
                    "{\"error\":\"Not found\",\"routes\":[\"/haptic/{waveform}\",\"/waveforms\",\"/health\"]}");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error handling HTTP request");
                try
                {
                    SendJson(response, 500, "{\"error\":\"Internal server error\"}");
                }
                catch
                {
                    // Response may already be closed
                }
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch
                {
                    // Ignore close errors
                }
            }
        }

        private static void SendJson(HttpListenerResponse response, int statusCode, string json)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private static string BuildWaveformsJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"waveforms\":");
            sb.Append(BuildWaveformArray());
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildWaveformArray()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (var i = 0; i < HapticEventRegistry.Waveforms.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(EscapeJson(HapticEventRegistry.Waveforms[i]));
                sb.Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string EscapeJson(string value) =>
            value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                _listenTask?.Wait(TimeSpan.FromSeconds(2));
                PluginLog.Info("HTTP server stopped");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error stopping HTTP server");
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
```

Write this to `src/HttpHapticServer.cs`.

**Step 2: Commit**

```bash
git add src/HttpHapticServer.cs
git commit -m "feat: add HttpHapticServer listening on 127.72.80.84:8080"
```

---

### Task 6: HaTTaPticPlugin — the plugin entry point

**Files:**
- Create: `src/HaTTaPticPlugin.cs`

**Step 1: Write HaTTaPticPlugin.cs**

Plugin lifecycle: load starts the registry and HTTP server, unload tears everything down.

```csharp
namespace Loupedeck.HaTTaPticPlugin
{
    using System;

    /// <summary>
    /// HaTTaPtic Plugin — HTTP-triggered haptic feedback for MX Master 4.
    /// Listens on http://127.72.80.84:8080/ for GET requests to trigger haptic waveforms.
    /// </summary>
    public class HaTTaPticPlugin : Plugin
    {
        private HapticEventRegistry _registry;
        private HttpHapticServer _httpServer;

        public override bool UsesApplicationApiOnly => true;
        public override bool HasNoApplication => true;

        public HaTTaPticPlugin()
        {
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);
        }

        public override void Load()
        {
            try
            {
                PluginLog.Info("HaTTaPtic plugin loading...");

                _registry = new HapticEventRegistry(this);
                _registry.RegisterAll();

                _httpServer = new HttpHapticServer(_registry);
                _httpServer.Start();

                PluginLog.Info("HaTTaPtic plugin loaded — listening on http://127.72.80.84:8080/");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load HaTTaPtic plugin");
            }
        }

        public override void Unload()
        {
            try
            {
                PluginLog.Info("HaTTaPtic plugin unloading...");

                _httpServer?.Stop();
                _httpServer?.Dispose();
                _httpServer = null;

                _registry = null;

                PluginLog.Info("HaTTaPtic plugin unloaded");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error unloading HaTTaPtic plugin");
            }
        }
    }
}
```

Write this to `src/HaTTaPticPlugin.cs`.

**Step 2: Commit**

```bash
git add src/HaTTaPticPlugin.cs
git commit -m "feat: add HaTTaPticPlugin entry point with lifecycle management"
```

---

### Task 7: Dockerfile and build script

**Files:**
- Create: `Dockerfile`
- Create: `build.sh`

**Step 1: Write Dockerfile**

The challenge: the build needs `PluginApi.dll` from the Logi Options+ installation.
Since Docker can't access `/Applications/`, we copy it in via the build context.
The build script handles extracting the DLL before running Docker.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy the PluginApi.dll that was extracted by build.sh
COPY .build/PluginApi.dll /opt/PluginApi/PluginApi.dll

# Copy project file and restore
COPY src/HaTTaPticPlugin.csproj ./src/
RUN dotnet restore src/HaTTaPticPlugin.csproj -p:PluginApiDir=/opt/PluginApi/

# Copy source code
COPY src/ ./src/

# Build
RUN dotnet build src/HaTTaPticPlugin.csproj -c Release -p:PluginApiDir=/opt/PluginApi/ --no-restore \
    && echo "Build successful"

# Output stage — just the build artifacts
FROM scratch AS output
COPY --from=build /src/bin/Release/ /output/
```

Write this to `Dockerfile`.

**Step 2: Write build.sh**

```bash
#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

PLUGIN_NAME="HaTTaPtic"
PLUGIN_API_SOURCE="/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll"
BUILD_DIR=".build"

echo "========================================"
echo "Building $PLUGIN_NAME (Docker)"
echo "========================================"

# Check for Docker
if ! command -v docker &> /dev/null; then
    echo ""
    echo "Error: Docker not found"
    echo "Install Docker Desktop from https://www.docker.com/products/docker-desktop/"
    exit 1
fi

# Extract PluginApi.dll for Docker context
echo ""
echo "Extracting PluginApi.dll..."
mkdir -p "$BUILD_DIR"

if [ -f "$PLUGIN_API_SOURCE" ]; then
    cp "$PLUGIN_API_SOURCE" "$BUILD_DIR/PluginApi.dll"
    echo "  -> Copied from $PLUGIN_API_SOURCE"
elif [ -f "$BUILD_DIR/PluginApi.dll" ]; then
    echo "  -> Using cached $BUILD_DIR/PluginApi.dll"
else
    echo ""
    echo "Error: PluginApi.dll not found"
    echo ""
    echo "Expected at: $PLUGIN_API_SOURCE"
    echo "Make sure Logi Options+ is installed."
    echo ""
    echo "Alternatively, copy PluginApi.dll manually to $BUILD_DIR/"
    exit 1
fi

# Build with Docker
echo ""
echo "Building in Docker..."
docker build --target output --output "type=local,dest=./bin/Release" -f Dockerfile .

echo ""
echo "Build complete!"
echo "Output: bin/Release/"
echo ""

# Package if logiplugintool is available
if command -v logiplugintool &> /dev/null; then
    echo "Creating plugin package..."
    logiplugintool pack "./bin/Release" "./$PLUGIN_NAME.lplug4"
    logiplugintool verify "./$PLUGIN_NAME.lplug4"
    echo ""
    echo "Package created: $PLUGIN_NAME.lplug4"
else
    echo "Note: logiplugintool not found — skipping .lplug4 packaging."
    echo "You can still install manually by copying bin/Release/ to the plugins directory."
fi

echo ""
echo "========================================"
echo "Installation"
echo "========================================"
echo ""
echo "Option A (with .lplug4):"
echo "  Double-click $PLUGIN_NAME.lplug4"
echo ""
echo "Option B (manual):"
echo "  cp -r bin/Release/ ~/Library/Application\ Support/Logi/LogiPluginService/Plugins/$PLUGIN_NAME/"
echo ""
echo "Then restart Logi Options+."
echo ""
echo "Test with:"
echo "  curl http://127.72.80.84:8080/health"
echo "  curl http://127.72.80.84:8080/haptic/knock"
echo ""
```

Write this to `build.sh`.

**Step 3: Make build.sh executable and commit**

```bash
chmod +x build.sh
git add Dockerfile build.sh
git commit -m "chore: add Docker-based build system"
```

---

### Task 8: Add .build/ to .gitignore and create README

**Files:**
- Modify: `.gitignore` — add `.build/` directory
- Create: `README.md`

**Step 1: Add .build/ to .gitignore**

Append to `.gitignore`:
```
# Build staging (PluginApi.dll copy)
.build/
```

**Step 2: Write README.md**

```markdown
# HaTTaPtic

HTTP-triggered haptic feedback for Logitech MX Master 4.

Send a curl request, the mouse vibrates.

## Usage

```bash
# Trigger a haptic waveform
curl http://127.72.80.84:8080/haptic/knock

# List available waveforms
curl http://127.72.80.84:8080/waveforms

# Health check
curl http://127.72.80.84:8080/health
```

## Available Waveforms

| Waveform | Feel |
|----------|------|
| `sharp_collision` | Sharp, precise click |
| `damp_collision` | Soft collision |
| `subtle_collision` | Very light tap |
| `sharp_state_change` | Clear state confirmation |
| `damp_state_change` | Soft state confirmation |
| `completed` | Completion / success |
| `angry_alert` | Strong warning |
| `happy_alert` | Positive notification |
| `knock` | Simple knock |
| `ringing` | Ringing notification |

## Requirements

- macOS 14+ (Sonoma)
- Logitech MX Master 4
- Logi Options+ installed and running
- Docker (for building)

## Build

```bash
./build.sh
```

Requires Docker. The build runs in a container — no local .NET SDK needed.

## Install

1. Build: `./build.sh`
2. Double-click `HaTTaPtic.lplug4`, or copy `bin/Release/` to:
   ```
   ~/Library/Application Support/Logi/LogiPluginService/Plugins/HaTTaPtic/
   ```
3. Restart Logi Options+

## How It Works

HaTTaPtic is a Logi Options+ plugin. It starts an HTTP server on `127.72.80.84:8080`
(HPT in ASCII — a unique loopback address to avoid port conflicts). When it receives
a GET request to `/haptic/{waveform}`, it triggers the corresponding haptic pattern
on the MX Master 4 via the Logi Options+ Plugin SDK.

## License

MIT
```

Write this to `README.md`.

**Step 3: Commit**

```bash
git add .gitignore README.md
git commit -m "docs: add README and update gitignore"
```

---

### Task 9: Verify build compiles in Docker

**Step 1: Run the build**

```bash
./build.sh
```

Expected: Docker builds the .NET project successfully, outputs to `bin/Release/`.

If the build fails, fix the issue and re-run.

**Step 2: Verify output exists**

```bash
ls -la bin/Release/bin/HaTTaPticPlugin.dll
```

Expected: The DLL file exists.

**Step 3: Commit any fixes if needed**

---

### Task 10: Test the plugin manually

This requires Logi Options+ running with the MX Master 4 connected.

**Step 1: Install the plugin**

```bash
# Create a link file for development
echo "$(pwd)/bin/Release/" > ~/Library/Application\ Support/Logi/LogiPluginService/Plugins/HaTTaPticPlugin.link
```

**Step 2: Restart Logi Options+**

```bash
open loupedeck:plugin/HaTTaPtic/reload
```

**Step 3: Test HTTP endpoints**

```bash
# Health check
curl -s http://127.72.80.84:8080/health
# Expected: {"status":"ok"}

# List waveforms
curl -s http://127.72.80.84:8080/waveforms
# Expected: {"waveforms":["sharp_collision","damp_collision",...]}

# Trigger haptic
curl -s http://127.72.80.84:8080/haptic/knock
# Expected: {"status":"ok","waveform":"knock"} + mouse vibrates

# Unknown waveform
curl -s http://127.72.80.84:8080/haptic/doesnotexist
# Expected: 404 with error message

# Unknown route
curl -s http://127.72.80.84:8080/foo
# Expected: 404 with available routes
```

**Step 4: Final commit if any adjustments were needed**

```bash
git add -A
git commit -m "fix: adjustments from manual testing"
```
