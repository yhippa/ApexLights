# ApexLights

A standalone Windows 11 x64 shift-light strip for iRacing. Download **ApexLights.exe** from the latest release, then double-click it. [Download ApexLights for Windows x64](https://github.com/yhippa/ApexLights/releases/latest/download/ApexLights.exe). The executable includes the .NET desktop runtime. You can move it to another folder and run it there.

## First drive

1. On first launch, **Automatic** runs a demo sweep while it waits for iRacing. The floating strip is the driving overlay. The larger window is its settings panel.
2. Click **Edit overlay** or press **Ctrl + Alt + L**. Drag the strip to your preferred monitor and drag its lower-right grip to resize it. Click **Lock overlay** or press the shortcut again. The locked strip passes mouse input through to the window beneath it.
3. For manual testing, choose **Telemetry → Demo** and move **Manual RPM**. Moving the slider stops the sweep. Set the four RPM values and click **Apply thresholds**, or turn the sweep back on. To open directly in Demo Mode from PowerShell, run `.\ApexLights.exe --demo` from the executable's folder.
4. For racing, select **Automatic** or **iRacing**, then run the simulator in borderless-windowed mode. **Automatic** switches to live data when the first valid session arrives. After a live disconnect, both modes extinguish the LEDs and wait to reconnect. The **Demo** selection stays in simulation until you change it.
5. **Hide settings** or close the settings window for an LED-only view. Double-click the ApexLights tray icon or launch the executable again to reopen settings. Right-click the tray icon for **Exit ApexLights**.

## Tune the strip

**Appearance** controls the segment count, shape, dimensions, spacing, four zone colors, fill direction, intensity, opacity, and housing. Disable **Dark strip housing** for transparent space between lenses. **Placement** includes scale, a reset to the primary monitor, and the option to start with settings hidden. Positions use physical desktop coordinates and sizes scale with monitor DPI.

The first LED activates at **First LED**. Progression is evenly spaced up to **Last LED**. Flashing starts at **Shift RPM** by default, even on cars whose shift cue comes before the last LED. Choose **Blink RPM** to follow the car's blink cue or **Off** for steady progression. The RPM offset moves the entire curve earlier or later. Flash frequency is in complete cycles per second.

When a session is connected, the Telemetry tab shows the current car's values. Use **Save car override** to retain a profile for that car and **Use car defaults** to remove it. Missing or inconsistent fields are repaired individually using the car's redline or its available shift values. Settings are saved to `%LOCALAPPDATA%\ApexLights\settings.json`.

## Build and test

Install the **.NET 10 x64 SDK** on the development machine. Run these commands in PowerShell from the repository root.

```powershell
dotnet build ApexLights.slnx -c Release
dotnet run --project ApexLights.Tests -c Release
dotnet run --project ApexLights -c Release -- --demo
```

The dependency-free test runner returns a nonzero exit code on failure. It checks LED boundaries, flash phase, invalid thresholds, JSON persistence, buffer decoding, and reconnects using a private named mapping. To also sample an existing iRacing recording, append `-- --recording "C:\path\to\session.ibt"` to the test command.

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

- **Waiting for iRacing** means no fresh valid telemetry has arrived. Enter a simulator session, select **iRacing** or **Automatic**, and check that the simulator's memory telemetry output is enabled (`irsdkEnableMem=1` in its `app.ini`). Run the sim and overlay as the same Windows user.
- **Strip is hidden over the game.** Use borderless-windowed mode. After a monitor change, choose **Placement → Center on primary display**.
- **Shortcut unavailable.** The footer shows the active shortcut. If the default is taken, ApexLights tries **Ctrl + Alt + Shift + L**. The settings and tray edit commands are always available.
- **Settings are damaged.** Exit from the tray, rename the settings JSON, and relaunch for defaults.
- **First launch takes longer.** The single-file build extracts bundled native libraries into the user's temporary .NET bundle cache. Windows needs write access to that cache and to the local settings folder.

