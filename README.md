<div align="center">

<img src="assets/AkariLogo.png" alt="Akari Tool" width="120"/>

# Akari Tool

**A clean WPF graphical front-end for the FR33THY "Ultimate" Windows tweaking scripts.**

Point-and-click access to the same optimizations, debloat steps, installers, and diagnostics —
no console menus, no typing numbers, one window.

</div>

---

> ⚠️ **Read this first.** Akari Tool modifies Windows system settings, the registry, services,
> drivers, and installed apps. Some tweaks are **advanced** and can affect stability or security
> (disabling Defender, the firewall, UAC, Spectre/Meltdown mitigations, etc.).
> **Create a restore point before using it, and only apply what you understand. Use at your own risk.**

---

## Table of contents

- [What it is](#what-it-is)
- [Features](#features)
- [Requirements](#requirements)
- [Running it](#running-it)
- [How the app works](#how-the-app-works)
- [Building from source](#building-from-source)
- [Updating the tweaks](#updating-the-tweaks)
- [Project structure](#project-structure)
- [FAQ](#faq)
- [Credits](#credits)
- [License & disclaimer](#license--disclaimer)

---

## What it is

Akari Tool wraps [FR33THY's **Ultimate**](https://github.com/FR33THYFR33THY/Ultimate) tweak
collection in a native Windows 11-styled GUI (WPF, Mica backdrop, dark theme). Ultimate ships as
dozens of standalone, menu-driven PowerShell scripts; Akari Tool exposes each one as a labelled
button grouped into tabs, so you can see everything at a glance and apply it with a click.

The whole app ships as **one self-contained file**, `akari.ps1`, which is *compiled* from the
source in this repo. There are no external dependencies to install — the GUI, styles, icon, and
logic are all embedded.

## Features

Eight tabs, mirroring Ultimate's structure:

| Tab | What's inside |
|-----|---------------|
| **Check** | PC stress test (OCCT) + drive/RAM/GPU checklist, and BIOS update/settings helper |
| **Refresh** | Factory reset, W10/W11 reinstall (Media Creation Tool), autounattend, driver/update blocking |
| **Setup** | BitLocker, memory compression, background apps, Edge & Store settings, pause updates, activation |
| **Installers** | One-click winget installs for launchers, browsers & apps (Steam, Discord, Chrome, …) each pre-debloated, plus GPU tools (Afterburner, NPI, CRU/SRE) |
| **Graphics** | DDU driver clean, driver install (updated / debloat), NVIDIA/AMD/Intel settings, HDCP, P0 state, MSI mode, DirectX & C++ runtimes |
| **Windows** | Taskbar/Start clean, context menu, black theme, debloat & privacy, power plan, timer resolution, write cache, device/network power, and more |
| **Hardware** | High-scaling-no-acceleration, monitor optimization, mouse/controller polling & overclock tests |
| **Advanced** | Defender, firewall, Spectre/Meltdown, DEP, services, MMAgent, NVMe driver, MPO, flip modes, ULPS, ReBar, keyboard shortcuts, SMT/affinity, WHQL bypass |

Buttons marked **★** are the recommended option for that row.

## Requirements

- **Windows 10 or 11** (Home / Pro / LTSC / IoT / Server)
- **Administrator** rights — the app self-elevates on launch
- **Internet access** — most installers and the heavier tweaks download or fetch content at run time
- **winget** (App Installer) — used by the Installers tab and several tweaks; ships with modern Windows
- Windows updates **unblocked** while installing apps (some tweaks can re-block them afterwards)

## Running it

### Option A — run the prebuilt file

Download `akari.ps1`, then in an **elevated** PowerShell / Terminal:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\path\to\akari.ps1"
```

(The app will prompt for admin if you didn't start elevated.)

### Option B — build & run from source

```powershell
# from the repo root
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Compile.ps1" -Run
```

`Compile.ps1` regenerates `akari.ps1` from the source files and `-Run` launches it as admin.

## How the app works

### One compiled file

`akari.ps1` is **generated**, never edited by hand. `Compile.ps1` concatenates the source in a
fixed order into the single script:

```
scripts/start.ps1        → admin elevation, WPF + DWM setup, shared $sync state
functions/private/*.ps1  → helpers (background runner, upstream-script launcher)
functions/public/*.ps1   → one file per tab, holding the button handlers
config/*.json            → embedded as $sync.configs.<name>  (optional)
assets/AkariLogo.png     → embedded as base64 (title-bar logo + window icon)
xaml/MainWindow.xaml     → embedded as the $inputXML here-string (the whole UI)
scripts/main.ps1         → parses the XAML, wires buttons, shows the window
```

### The UI is XAML, the logic is functions

`xaml/MainWindow.xaml` defines the entire window (styles, tabs, cards, buttons). Every named
button auto-wires to a matching function by convention:

```
<Button Name="BtnFirewallDisable" .../>   →   function Invoke-BtnFirewallDisable { ... }
```

`main.ps1` loops over every `Btn*` control and hooks its click to `Invoke-<name>`, so adding a
button + a same-named function is all it takes to add a feature. Long-running actions run on a
background runspace (`Invoke-RunInBackground`) so the window never freezes, and report progress in
the status bar.

### Two kinds of handler — this is the key design

To stay **1:1 with Ultimate** while remaining **easy to maintain**, handlers come in two flavours:

- **Delegated** — heavy, interactive, or frequently-changing tweaks call
  `Invoke-UltimateScript -Path "<folder>/<file>.ps1"`, which launches the **live upstream script**
  from FR33THY's GitHub in an elevated console. These are Ultimate's exact logic *by construction*
  and **update themselves** whenever Ultimate changes — nothing to re-port.

- **Inline** — simple, stable registry toggles and the per-app installers are implemented directly
  in the `Invoke-*.ps1` files as one-click actions (no console menu). These are the only parts you
  ever hand-edit.

## Updating the tweaks

Most of the app tracks upstream automatically. You only touch the inline handlers.

1. **Delegated handlers** — nothing to do; they fetch the current Ultimate script at click-time.
2. **Inline handlers** — when an upstream tweak changes, open the matching Ultimate script and copy
   **only its core commands** (skip the admin-elevation header, the `Write-Host` menu, the
   `while/switch` loop, `Pause`, and `exit`) into the corresponding `Invoke-Btn*` function.

   | Ultimate folder | Source file to edit |
   |-----------------|---------------------|
   | `1 Check`       | `functions/public/Invoke-Check.ps1` |
   | `2 Refresh`     | `functions/public/Invoke-Refresh.ps1` |
   | `3 Setup`       | `functions/public/Invoke-Setup.ps1` |
   | `4 Installers`  | `functions/public/Invoke-Installers.ps1` |
   | `5 Graphics`    | `functions/public/Invoke-Graphics.ps1` |
   | `6 Windows`     | `functions/public/Invoke-Windows.ps1` |
   | `7 Hardware`    | `functions/public/Invoke-Hardware.ps1` |
   | `8 Advanced`    | `functions/public/Invoke-Advanced.ps1` |

3. **Rebuild:** `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Compile.ps1"`.

> Never edit `akari.ps1` directly — your changes would be overwritten on the next compile.

## Project structure

```
akari-tool/
├─ akari.ps1              # COMPILED output — do not edit by hand
├─ Compile.ps1           # builds akari.ps1 from the sources below (-Run to launch)
├─ README.md
├─ assets/               # logo (.png/.ico) and images, embedded at compile time
├─ config/               # optional JSON configs, embedded into $sync.configs
├─ scripts/
│  ├─ start.ps1          # elevation, WPF/DWM init, shared state
│  └─ main.ps1           # XAML parse, button wiring, window show
├─ functions/
│  ├─ private/
│  │  ├─ Invoke-RunInBackground.ps1   # non-blocking runspace runner
│  │  └─ Invoke-UltimateScript.ps1    # launches a live upstream Ultimate script
│  └─ public/
│     ├─ Invoke-Check.ps1
│     ├─ Invoke-Refresh.ps1
│     ├─ Invoke-Setup.ps1
│     ├─ Invoke-Installers.ps1
│     ├─ Invoke-Graphics.ps1
│     ├─ Invoke-Windows.ps1
│     ├─ Invoke-Hardware.ps1
│     ├─ Invoke-Advanced.ps1
│     └─ Invoke-Missing.ps1           # extra Windows/Advanced/Installer handlers
└─ xaml/
   └─ MainWindow.xaml   # the entire UI (styles, tabs, buttons)
```

## FAQ

**Does it need Ultimate downloaded to work?**
No. The delegated buttons fetch the upstream script over the internet at click-time; the inline
ones are self-contained. You only need Ultimate on disk if you're *editing* the inline handlers.

**Why do some buttons open a black console window?**
Those are the delegated (heavy/interactive) tweaks — they run Ultimate's original menu-driven
script so behaviour is identical to running Ultimate directly. Pick the option in the console.

**A button did nothing / needs a reboot.**
Many tweaks apply on next sign-out or restart. Check the status bar at the bottom of the window.

**Is it safe?**
It's as safe as the tweaks you choose to run. The Advanced tab in particular contains
security-reducing options. Make a restore point first.

## Credits

- **Tweaks, scripts & research:** [FR33THY](https://github.com/FR33THYFR33THY) —
  [Ultimate](https://github.com/FR33THYFR33THY/Ultimate) and
  [Ultimate-Files](https://github.com/FR33THYFR33THY/Ultimate-Files).
  Watch the guide: <https://youtu.be/zwPEDXteJYQ>
- **Akari Tool GUI:** isleap.

This project is an independent front-end and is not affiliated with or endorsed by FR33THY.

## License & disclaimer

Provided **as-is, without warranty of any kind**. The authors are not responsible for any damage,
data loss, instability, or security exposure resulting from its use. You are solely responsible for
what you run on your system.

No license is set yet — add a `LICENSE` file to define how others may use the code (MIT is a common
choice for a project like this). Note that the tweaks themselves originate from FR33THY's Ultimate;
respect its terms when redistributing.
