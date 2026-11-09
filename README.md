# ApexLights

A standalone Windows 11 x64 shift-light strip for iRacing. Download **ApexLights.exe** from the latest release, then double-click it. [Download ApexLights for Windows x64](https://github.com/yhippa/ApexLights/releases/latest/download/ApexLights.exe). The executable includes the .NET desktop runtime. You can move it to another folder and run it there.

## First drive

1. On first launch, **Automatic** runs a demo sweep while it waits for iRacing. The floating strip is the driving overlay. The larger window is its settings panel.
2. Click **Edit overlay** or press **Ctrl + Alt + L**. Drag the strip to your preferred monitor and drag its lower-right grip to resize it. Click **Lock overlay** or press the shortcut again. The locked strip passes mouse input through to the window beneath it.
3. For manual testing, choose **Demo / Testing → Input → Demo** and move **Manual RPM**. Moving the slider stops the sweep. Set the four RPM values and click **Apply thresholds**, or turn the sweep back on. To open directly in Demo Mode from PowerShell, run `.\ApexLights.exe --demo` from the executable's folder.
4. For racing, select **Automatic** or **iRacing**, then run the simulator in borderless-windowed mode. **Automatic** switches to live data when the first valid session arrives. After a live disconnect, both modes extinguish the LEDs and wait to reconnect. The **Demo** selection stays in simulation until you change it.
5. **Hide settings** or close the settings window for an LED-only view. Double-click the ApexLights tray icon or launch the executable again to reopen settings. Right-click the tray icon for **Exit ApexLights**.

## Settings and preview

The six settings sections share one live preview. In **Overlay Appearance → Theme**, choose **Dark** (default) or **Light**. The **Dark track / Light track** selector above the preview lets you test contrast independently of the settings theme. Both choices are saved. Expanding the thresholds and individual flags reveals finer controls.

Settings uses the Fluent controls included in .NET 10 WPF. Its system accent, keyboard focus and high-contrast resources apply to the settings window. The HUD draws its own materials and optics. [Microsoft's WPF Fluent theme](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)

## Tune the strip

In **Demo / Testing → Input → Demo**, the **Demo look** applies to every scenario and the manual controls. **Cockpit** is the default and uses the neon palette; **My setup** previews your saved driving appearance. Switching scenarios changes only the simulated inputs. **Full lights** stages fourth gear, the full LED bank, two cars on the right, and a brief green-flag event.

To use that appearance in a race, click **Overlay Appearance → Apply cockpit preset**. The preset turns on the gear pod, status panel and side indicators, and selects twelve blade LEDs with green, gold, red and blue zones. Your thresholds, car overrides, flash settings, position and scale are preserved. The LEDs and gear share one translucent pill with a single divider next to the gear. The optional side marks sit at its ends. Amber indicates an actual alongside report. With the housing off, inactive endcaps disappear.

**Shift Lights** controls the segment count, shape, lens dimensions, spacing, four zone colors, fill direction, intensity, and flash behavior. Use **Inactive LEDs** to set the unlit lens brightness, including fully dark at zero. In **Overlay Appearance**, disable **Dark strip housing** for transparent space between lenses. **Center on this monitor** centers the RPM-and-gear pill 30% down the display, including after scale changes. Dragging switches to a saved manual position. This page also includes scale and the option to start with settings hidden. Positions use physical desktop coordinates and sizes scale with monitor DPI.

The first LED activates at **First LED**. Progression is evenly spaced up to **Last LED**. Flashing starts at **Shift RPM** by default, even on cars whose shift cue comes before the last LED. Choose **Blink RPM** to follow the car's blink cue or **Off** for steady progression. The RPM offset moves the entire curve earlier or later. Flash frequency is in complete cycles per second.

When a session is connected, **Demo / Testing** shows the current car's values. Use **Save car override** to retain a profile for that car and **Use car defaults** to remove it. Missing or inconsistent fields are repaired individually using the car's redline or its available shift values. Settings are saved to `%LOCALAPPDATA%\ApexLights\settings.json`.

## Race awareness

Open **Gear Indicator**, **Racing Flags**, or **Proximity Awareness** to configure each channel. A 1.0 settings file opens with all three disabled and retains its placement, scale, colors and car overrides. Exit the previous copy from the tray before launching a newer build.

- **Gear** shows R, N, or the forward gear. Choose its typeface, size and left, right, or center position. Center placement opens a gap between LED groups and keeps every emitter visible.
- **Status panel** reserves a separate area to the right of the pill. Enable it in **Racing Flags** and choose a symbol or a labeled tile. The reserved area stays quiet when clear. Red, black and repair take precedence over active cautions, then the pit limiter, then informational and finish notices. A held warning clears as soon as the SDK condition clears. Manual dismissal silences only that occurrence.
- **Proximity** shows one or two chevrons or lamps at the corresponding edge. Both edges can light together. These are the SDK spotter’s discrete alongside reports. Clear, off, missing and unknown states show only the resting endcap when the housing is enabled.

**Visibility.** Amber chevrons or lamps light up on the occupied side. Flags share one fabric silhouette between the status panel and the floating display. Checkered and repair retain their distinctive patterns. In their respective sections, adjust **Side cue size** and **Flag size** independently from 75% to 175%. The strip reserves room for enabled cues, so its LEDs hold their size and position when alerts come and go. For a quick size check in Demo Mode, hold a yellow flag and select **Both**, then try the settings from your driving position.

The SDK spotter gives the side and vehicle count. Its report contains neither the identity of the overlapping car nor its longitudinal offset. Per-car lap progress cannot reliably identify which car triggered a particular side warning in traffic, so the chevrons point sideways. [SDK spotter enum](https://github.com/SIMRacingApps/SIMRacingAppsSIMPluginiRacing/blob/master/irsdk/irsdk_defines.h)

**Peripheral cues.** Enable **Proximity Awareness → Show peripheral bands** for large amber side markers on the strip's monitor. The cues sit inside a centered area up to four-thirds of the monitor height wide. **Edge inset** moves them farther inward. Enable **Racing Flags → Show floating flag** for a translucent flag near the forward view. A new caution or critical warning appears there for the notice duration, or until it clears. A held warning remains available in the strip. Use Edit Mode to position the strip clear of your virtual mirror, then set **Vertical position** to place the flag below it. Adjust position, size, and shared cue opacity in those sections. Both options start off for saved driving setups. The **Cockpit** demo enables them so **Competing alerts** shows both bands and a central flag together.

**Events and active conditions.** GREEN and FINISH appear for the configured **Notice duration** on an observed rising edge. A held bit cannot restart that timer. Joining, reconnecting or changing sessions establishes a baseline for events and immediately shows any active warning. WHITE follows the saved timed/until-clear preference. LIMITER uses the pit-speed-limiter bit in the optional EngineWarnings channel and clears when the bit clears. Pit-lane presence and the engine rev limiter do not activate it. RPM, gear and spotter marks have independent regions. [SDK warning bits and flag definitions](https://github.com/SIMRacingApps/SIMRacingAppsSIMPluginiRacing/blob/master/irsdk/irsdk_defines.h)

## Test a driving situation

In **Demo / Testing**, select **Demo** for the input. Under **Driving scenarios**, click any scenario. Each press stages a fresh occurrence. All scenarios use the current thresholds, RPM offset, styles and priority logic.

| Scenario | What to look for |
|---|---|
| Full lights | All LEDs at the shift point, fourth gear, a green event and two cars alongside right. |
| Normal driving | Mid-range RPM, fourth gear and an empty status well. |
| Approach shift | A sweep starts just below the selected shift cue and crosses it. |
| Side by side | A single steady marker at the left edge. |
| Local yellow | Waving yellow in the right status slot. |
| Green transition / Finish | A deliberate clear-to-raised edge displays GREEN or FINISH once, then goes quiet even if the bit stays on. |
| Blue flag / Black flag | A warning remains visible while the condition is active. |
| Pit limiter | LIMITER and a gauge symbol remain visible until disengagement. |
| Repair required | Black flag with an orange disc and the REPAIR label. |
| Competing alerts | RPM at the shift cue, both sides occupied, with black ahead of repair, waving yellow, limiter and finish. |

**Clear alerts** clears flags, limiter and proximity while retaining gear, RPM or sweep, connection, demo look and scale. **Test scale** previews 75%, 100% or 150% on the actual strip. Choose **Saved** to return to your configured size. At **Saved** scale, Demo and live input use the same window bounds. The cockpit art fits inside the saved strip width. Live mode uses your configured appearance. The spotter reports left, right, occupied on both sides, two left, or two right. The both-sides state gives no separate counts for those sides.

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

The existing SDK reader samples optional `Gear`, `SessionFlags` `CarLeftRight` and `EngineWarnings` channels from the same tick as RPM. A missing or invalid optional channel clears just its indication. `IndicatorPolicy` tracks flag edges, expiry, dismissal and session identity. It emits a compact frame for the gear, flag and side regions alongside the original shift-light frame. A new SDK attachment, car or session re-arms alerts. Demo Mode supplies the same signal type using local controls.

The reader opens the SDK mapping with read access and waits on the data-valid event with synchronization rights. It resolves typed RPM and gear offsets from the variable descriptors, checks bounds, and verifies the buffer tick after sampling. A session revision refreshes driver identity and car-specific thresholds. Verified against the [iRacing SDK header](https://github.com/SIMRacingApps/SIMRacingAppsSIMPluginiRacing/blob/master/irsdk/irsdk_defines.h), [SDK reader](https://github.com/SIMRacingApps/SIMRacingAppsSIMPluginiRacing/blob/master/irsdk/irsdk_utils.cpp), and the [SDK telemetry field list](https://us.v-cdn.net/6034148/uploads/8DD84H30FIC8/telemetry-11-23-15.pdf).

## Troubleshooting

- **Waiting for iRacing** means no fresh valid telemetry has arrived. Enter a simulator session, select **iRacing** or **Automatic**, and check that the simulator's memory telemetry output is enabled (`irsdkEnableMem=1` in its `app.ini`). Run the sim and overlay as the same Windows user.
- **Strip is hidden over the game.** Use borderless-windowed mode. After a monitor change, choose **Overlay Appearance → Center on this monitor**.
- **Shortcut unavailable.** The footer shows the active shortcut. If the default is taken, ApexLights tries **Ctrl + Alt + Shift + L**. The settings and tray edit commands are always available.
- **Settings are damaged.** Exit from the tray, rename the settings JSON, and relaunch for defaults.
- **First launch takes longer.** The single-file build extracts bundled native libraries into the user's temporary .NET bundle cache. Windows needs write access to that cache and to the local settings folder.

