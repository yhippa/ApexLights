# ApexLights

A standalone Windows 11 x64 shift-light strip for iRacing. Download **ApexLights.exe** from the latest release, then double-click it. [Download ApexLights for Windows x64](https://github.com/yhippa/ApexLights/releases/latest/download/ApexLights.exe). The executable includes the .NET desktop runtime. You can move it to another folder and run it there.

## First drive

Start ApexLights and enter an iRacing session in borderless-windowed mode. Live input waits for fresh telemetry. Double-click the tray icon to open settings. Use **Edit overlay** or **Ctrl + Alt + L** to drag and resize the pill, then lock it for click-through driving. Closing settings hides the panel. Use the tray menu to exit.

**Shift Lights** controls LED count, shape, colors, brightness, progression, flash trigger, flash frequency, and RPM offset. The integrated gear shows reverse, neutral, or the current forward gear. **Overlay Appearance** controls scale, opacity and placement. Centering places the pill 30% down its monitor.

**Input / Calibration** shows the current car and its session thresholds. Save an override to tune that car or restore its defaults. For offline testing run `ApexLights.exe --demo` or select Demo. Use the RPM sweep, manual RPM, gear and threshold controls to check the same layout used on track.

Version 1.3 disables peripheral traffic and flag displays, including in older saved profiles. The flag artwork, priority rules and SDK decoding remain available in source. Saved LED appearance, gear preferences, scale, and car overrides are reused. Settings are stored in `%LOCALAPPDATA%\ApexLights\settings.json`.

## Build and test

Install the **.NET 10 x64 SDK** on the development machine. Run these commands in PowerShell from the repository root.

```powershell
dotnet build ApexLights.slnx -c Release
dotnet run --project ApexLights.Tests -c Release
dotnet run --project ApexLights -c Release -- --demo
```

The Windows tests render the gear at multiple scales and DPI settings and check transparent flag and chevron artwork. The test runner covers settings upgrades, both themes, peripheral placement at multiple resolutions and scales, and floating alert timing. It returns a nonzero exit code on failure. It checks LED boundaries, flash phase, invalid thresholds, JSON persistence, buffer decoding, and reconnects using a private named mapping. To also sample an existing iRacing recording, append `-- --recording "C:\path\to\session.ibt"` to the test command.

Publish a single self-contained executable:

```powershell
dotnet publish ApexLights/ApexLights.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
  -o publish/win-x64
```

## Implementation

`ApexLights.Core` contains SDK acquisition, shift-light evaluation, and JSON storage. `ApexLights` contains the WPF application, native overlay window, tray, demo source, and a retained drawing surface shared by the strip and its settings preview.

The reader opens the SDK mapping with read access and waits on the data-valid event with synchronization rights. It resolves typed RPM and gear offsets from the variable descriptors, checks bounds, and verifies the buffer tick after sampling. A session revision refreshes driver identity and car-specific thresholds. Verified against the [iRacing SDK header](https://github.com/SIMRacingApps/SIMRacingAppsSIMPluginiRacing/blob/master/irsdk/irsdk_defines.h), [SDK reader](https://github.com/SIMRacingApps/SIMRacingAppsSIMPluginiRacing/blob/master/irsdk/irsdk_utils.cpp), and the [SDK telemetry field list](https://us.v-cdn.net/6034148/uploads/8DD84H30FIC8/telemetry-11-23-15.pdf).

## Troubleshooting

- **Waiting for iRacing** means no fresh valid telemetry has arrived. Enter a simulator session, select **iRacing**, and check that the simulator's memory telemetry output is enabled (`irsdkEnableMem=1` in its `app.ini`). Run the sim and overlay as the same Windows user.
- **Strip is hidden over the game.** Use borderless-windowed mode. After a monitor change, choose **Overlay Appearance → Center on this monitor**.
- **Shortcut unavailable.** The footer shows the active shortcut. If the default is taken, ApexLights tries **Ctrl + Alt + Shift + L**. The settings and tray edit commands are always available.
- **Settings are damaged.** Exit from the tray, rename the settings JSON, and relaunch for defaults.
- **First launch takes longer.** The single-file build extracts bundled native libraries into the user's temporary .NET bundle cache. Windows needs write access to that cache and to the local settings folder.

