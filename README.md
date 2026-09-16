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

Tabs follow FR33THY Ultimate's own section order (1–8), plus a Home landing and a bonus Individual Tweaks tab:

| Tab | What's inside |
|-----|---------------|
| **Home** | Welcome + safety, one-click restore point, live "This PC" info, recommended-path shortcuts to sections 1–8, Desktop shortcut (online/offline), credits |
| **1 · Check** | BIOS update & settings, PC stability check (OCCT) |
| **2 · Refresh** | Factory reset, W10/W11 reinstall, autounattend, local account, block/unblock update drivers, network driver / To BIOS |
| **3 · Setup** | BitLocker, memory compression, background apps, keys/activation, convert to Pro, date/language/region, startup apps, Edge/Store settings, pause updates |
| **4 · Installers** | Winget installs for launchers, browsers & apps (each pre-debloated) + GPU tools |
| **5 · Graphics** | DDU driver clean, driver install/debloat (NVIDIA/AMD/Intel), GPU settings, HDCP, P0, MSI mode, DirectX/C++, resolution & HAGS |
| **6 · Windows** | Taskbar/Start layout & shortcuts, context menu, black theme, bloatware removal/checks + native reinstall (Store, UWP, OneDrive, Snipping, RDC, winget), Widgets/Copilot/Game Bar/Edge, Control Panel/Notepad/Sound, performance (power plan, timer, write cache, device/network power, IPv4), Game Mode/Pointer/Scaling, UAC, Defender Optimize, Autoruns/Cleanup/Restore Point/Core Isolation |
| **7 · Hardware** | Higher scaling (no accel), monitor optimization, background polling cap, mouse/controller polling tests, controller overclock, bufferbloat test, PC build guide |
| **8 · Advanced** | Use-with-care: Defender disable, firewall, Spectre/Meltdown, DEP, download warning, services, MMAgent, NVMe driver, shell/mobsync, MPO/flip/ULPS/ReBar, keyboard shortcuts, SMT/Core 1 Thread 1/Priority, WHQL bypass |
| **Individual Tweaks** | Scheduling (SvcHost Split Threshold, Win32 Priority Separation) plus 169 granular Control Panel tweaks from Ultimate — grouped, collapsed by default, Optimize/Default per row |

Buttons marked **★** are the recommended option for that row.

## Requirements

- **Windows 10 or 11** (Home / Pro / LTSC / IoT / Server)
- **Administrator** rights — the app self-elevates on launch
- **Internet access** — most installers and the heavier tweaks download or fetch content at run time
- **winget** (App Installer) — used by the Installers tab and several tweaks; ships with modern Windows
- Windows updates **unblocked** while installing apps (some tweaks can re-block them afterwards)

## Running it

### Option A — one-line install (recommended)

Paste this into an **elevated** PowerShell / Terminal (Run as administrator):

```powershell
iwr https://github.com/isleap9/Akari-Tool/raw/refs/heads/main/IWR.ps1 -useb | iex
```

This downloads the repo, drops an **`Akari-Tool`** folder on your Desktop, unblocks the files, and
opens it — the same bootstrap flow as FR33THY's Ultimate. Then run `akari.ps1` from that folder.
`IWR.ps1` self-elevates, so it re-prompts for admin if you didn't start elevated.

### Option B — run the prebuilt file

Download `akari.ps1`, then in an **elevated** PowerShell / Terminal:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\path\to\akari.ps1"
```

(The app will prompt for admin if you didn't start elevated.)

### Option C — build & run from source

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

- **Embedded console scripts** — a few interactive, menu-driven tweaks (SMT/HT, Core 1 Thread 1,
  Priority, Bloatware) aren't worth rebuilding as native WPF flows. Their `.ps1` files live in
  `assets/text/` and are **baked into `akari.ps1`** at compile time; the handler calls
  `Invoke-ConsoleScript -Asset "<name>"`, which decodes the embedded script to a temp file and
  launches it in an elevated console. Nothing is downloaded and there is no external folder to carry
  — the app is fully self-contained and entirely under your control. This is also where you add your
  **own** menu scripts.

- **Inline** — simple, stable registry toggles and the per-app installers are implemented directly
  in the `Invoke-*.ps1` files as one-click actions (no console menu). These are hand-edited too.

## Updating the tweaks

Everything lives in this repo — nothing tracks upstream automatically, so you decide when and what
to change.

1. **Embedded console scripts** — when FR33THY updates one (or you write your own), drop the `.ps1`
   into `assets/text/<name>.ps1` and, for a new one, add a handler that calls
   `Invoke-ConsoleScript -Asset "<name>"`. Recompile and it's baked in.
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
├─ IWR.ps1               # one-line installer: downloads the repo to the Desktop
├─ README.md
├─ assets/               # logo (.png/.ico) and images, embedded at compile time
├─ config/               # optional JSON configs, embedded into $sync.configs
├─ scripts/
│  ├─ start.ps1          # elevation, WPF/DWM init, shared state
│  └─ main.ps1           # XAML parse, button wiring, window show
├─ functions/
│  ├─ private/
│  │  ├─ Invoke-RunInBackground.ps1   # non-blocking runspace runner
│  │  └─ Invoke-ConsoleScript.ps1     # runs an embedded menu-driven script (elevated console)
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
