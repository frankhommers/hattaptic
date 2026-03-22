# HaTTaPtic Design

## Summary

HaTTaPtic is a Logi Options+ plugin that exposes MX Master 4 haptic feedback via HTTP.
Send a GET request, the mouse vibrates.

```
curl http://127.72.80.84:8080/haptic/knock
```

## Architecture

```
┌──────────────┐    HTTP GET     ┌──────────────────┐   PluginEvents    ┌──────────────┐
│   curl /     │ ─────────────> │    HaTTaPtic      │ ───────────────> │  MX Master 4 │
│   script /   │  127.72.80.84  │  (Logi Options+   │   RaiseEvent()   │   (haptic     │
│   webhook    │    :8080       │   Plugin)          │                  │    motor)     │
└──────────────┘                └──────────────────┘                   └──────────────┘
```

The plugin runs inside Logi Options+ and uses the Loupedeck Plugin SDK to trigger
haptic waveforms on the MX Master 4. An embedded HTTP server listens on a unique
loopback address (`127.72.80.84` — "HPT" in ASCII) to avoid port conflicts.

## API

| Route                  | Method | Description                    | Response                                      |
|------------------------|--------|--------------------------------|-----------------------------------------------|
| `/haptic/{waveform}`   | GET    | Trigger a haptic waveform      | `{"status":"ok","waveform":"knock"}`           |
| `/waveforms`           | GET    | List available waveforms       | `{"waveforms":["knock","sharp_collision",...]}` |
| `/health`              | GET    | Health check                   | `{"status":"ok"}`                              |

### Available Waveforms

| Category      | Waveforms                                          |
|---------------|-----------------------------------------------------|
| Precision     | `sharp_collision`, `damp_collision`, `subtle_collision` |
| Progress      | `sharp_state_change`, `damp_state_change`, `completed` |
| Notifications | `angry_alert`, `happy_alert`, `knock`, `ringing`       |

Intensity is not configurable per-request. Each waveform has a fixed pattern and
intensity defined by Logitech. Choose the appropriate waveform for the desired effect.

## Project Structure

```
HaTTaPtic/
├── src/
│   ├── HaTTaPticPlugin.cs          # Plugin entry point, lifecycle
│   ├── HttpHapticServer.cs         # HTTP listener on 127.72.80.84:8080
│   ├── HapticEventRegistry.cs      # Registration of haptic events
│   ├── HaTTaPticPlugin.csproj      # .NET 8 project file
│   └── package/
│       ├── metadata/
│       │   ├── LoupedeckPackage.yaml
│       │   └── Icon256x256.png
│       └── events/
│           ├── DefaultEventSource.yaml
│           └── extra/
│               └── eventMapping.yaml
├── HaTTaPtic.sln                   # Solution file
├── Dockerfile                      # Build environment (no local .NET needed)
├── build.sh                        # Docker-based build script
├── .gitignore
└── README.md
```

## Components

### HaTTaPticPlugin.cs

Plugin lifecycle management. On `Load()`: creates the `HapticEventRegistry`, registers
all waveforms, starts the `HttpHapticServer`. On `Unload()`: stops the HTTP server,
cleans up resources.

### HttpHapticServer.cs

Runs an `HttpListener` on `http://127.72.80.84:8080/`. Parses incoming URLs, matches
waveform names, calls `HapticEventRegistry.Trigger()`. Returns JSON responses with
appropriate HTTP status codes (200 OK, 404 for unknown waveforms, 500 for errors).

No debouncing needed — HTTP requests are inherently slower than OSC/UDP messages,
and the caller controls the rate.

### HapticEventRegistry.cs

Registers all supported waveforms as Logi Options+ plugin events via
`Plugin.PluginEvents.AddEvent()`. Provides a `Trigger(waveformName)` method that
calls `Plugin.PluginEvents.RaiseEvent()`.

### eventMapping.yaml

Maps event names to MX Master 4 waveform identifiers. Same format as ReaperHaptic.
Each waveform name (e.g., `knock`) maps directly to a Logitech haptic waveform.

## Build

Docker-based build to avoid requiring a local .NET 8 SDK installation:

```bash
./build.sh
# Produces: HaTTaPtic.lplug4
```

The Dockerfile uses the .NET 8 SDK image to compile the plugin and package it.

## Installation

1. Build: `./build.sh`
2. Install: double-click `HaTTaPtic.lplug4` or copy to
   `~/Library/Application Support/Logi/LogiPluginService/Plugins/`
3. Restart Logi Options+

## Requirements

- macOS 14+ (Sonoma)
- Logitech MX Master 4 with haptic feedback
- Logi Options+ installed and running
- Docker (for building only)

## Inspiration

Architecture and SDK usage inspired by [ReaperHaptic](https://github.com/b451c/ReaperHaptic)
v1.1.3 by falami.studio. Key difference: HTTP instead of OSC/UDP, no REAPER dependency,
generic haptic trigger for any use case.
