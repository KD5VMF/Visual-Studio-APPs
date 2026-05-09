# Visual Studio APPs

**Visual Studio APPs** is a collection of Windows / Visual Studio / C# projects made for fun, learning, experiments, graphics, simulations, AI ideas, science-style visual tools, and home-lab utilities.

This repo is meant to help people explore code, build projects, learn by example, and try interesting desktop applications without needing a huge paid software stack.

---

# Programs In This Repository

## AICreatureLab_v1.0.3

An AI creature simulation / experiment project focused on creature behavior, learning ideas, movement, and visual simulation.  
Good for experimenting with autonomous agents and artificial life concepts.

---

## AtomPlayground_v10

A scientifically inspired atom / particle / physics visualization playground.  
This project is aimed at visual learning, experimentation, and making science-style concepts easier to see on screen.

---

## BioGenesisX_v15b

A larger biological / artificial-life style simulation project.  
Focused on evolving life-like behavior, survival pressure, population behavior, and long-running simulated systems.

---

## DimExplorer

A dimension-exploration style C# WinForms project.  
Designed as a visual experiment for exploring space, dimensions, graphics, and interactive ideas.

---

## GreatFluidDynamics_Rebuilt

A fluid-dynamics visualization project rebuilt for better structure and usability.  
Useful for experimenting with visual flow, movement, and simulation-style graphics.

---

## GreatFluidDynamics_Rebuilt_v2

Second rebuilt version of the fluid-dynamics project.  
Includes additional fixes, improvements, and newer experiment work compared to the first rebuilt version.

---

## HelixSolarShow_Project_v5

A solar-system / helix-style visualization project.  
Built for showing motion, space, orbital-style movement, and visually interesting solar-system concepts.

---

## LifeForgeAccelerated v9

An accelerated life / evolution simulation project.  
Focused on fast-running simulation behavior, life-like rules, and visual experimentation.

---

## LifeForgeAccelerated_autoevolution_persistence

A LifeForge version focused on auto-evolution and persistence.  
Designed so simulation progress can continue, save, and build on previous learning or state.

---

## LifeForgeAccelerated_gpu_multithreaded_final_fix2

A higher-performance LifeForge version using GPU / multithreaded ideas where practical.  
Focused on making the simulation work harder and run faster on stronger systems.

---

## NewtonsCradleStudio_v3

A Newton’s cradle physics / graphics project.  
Good for learning physics visualization, motion, timing, collision-style behavior, and clean desktop graphics.

---

## PFLT14 — pfSense Live Telemetry Rev 14

PFLT14 is a full-screen Windows dashboard for watching a pfSense router/firewall in real time using SNMP.

It was built to be simple for a home lab: no SSH scraping, no pfSense web login, and no paid monitoring stack required.

The dashboard shows live traffic, selected-interface stats, link speed, byte counters, latency/probe health, retry status, peak values, and multiple graph-style cards in one clean screen.

### PFLT14 Features

- Real-time pfSense SNMP telemetry
- Fullscreen dashboard UI
- Automatic Mbps/Gbps/Tbps scaling
- Automatic byte-unit scaling
- Interface discovery and selection
- Retry/reconnect handling
- Peak tracking and graph-style cards
- CSV logging and screenshot support
- Saved settings and persistent monitored-port selection
- Long-running stable monitoring behavior

### What PFLT14 does

- Reads pfSense interface counters through SNMP v2c.
- Lets you choose the monitored interface/port from the dashboard.
- Saves the selected port and settings under `Documents\PFLT14`.
- Auto-scales traffic display from Mbps to Gbps/Tbps when needed.
- Auto-scales byte counters from Bytes to KBytes/MBytes/GBytes/TBytes.
- Handles counter resets, 32-bit rollover, and impossible one-sample spikes.
- Corrects common bad 5G link-speed reporting where some interfaces report `50 Gbps` instead of `5 Gbps`.
- Includes reconnect/retry behavior for more stable long-running monitoring.
- Provides debug copy/save, logs, CSV output, screenshots, and a fullscreen/window toggle.

### PFLT14 Requirements

#### PC

- Windows 10 or Windows 11.
- .NET 8 SDK, recommended for building from source.
- Network access from the PC to pfSense on UDP port `161`.

#### pfSense

- pfSense with SNMP enabled.
- SNMP v2c community string configured.
- Firewall rule allowing the dashboard PC to reach pfSense on UDP `161`.

PFLT14 does **not** need pfSense SSH access, web GUI credentials, or admin password storage.

### PFLT14 Quick Start

1. Download or clone this repository.
2. Open the folder:

```text
src\PFLT14
```

3. Double-click:

```text
build_and_run.cmd
```

4. In the app, open **Settings**.
5. Enter your pfSense address, SNMP community string, and port.
6. Use **Test / Discover** in settings, then choose the correct interface/port.

Default example values are:

```text
Router: 192.168.1.1
SNMP Port: 161
Community: public
```

Change the community string to match your pfSense setup.

Do not publish your real private community string in screenshots, commits, or bug reports.

### Recommended pfSense SNMP Setup

In pfSense:

1. Go to **Services → SNMP**.
2. Enable SNMP.
3. Set a community string.
4. Limit access to your trusted LAN when possible.
5. Add or confirm a firewall rule that permits the monitoring PC to reach pfSense on UDP `161`.

For safety, use a unique community string instead of `public`.

### PFLT14 Security Notes

SNMP v2c is plaintext on the network.

For best security:

- Use a custom community string.
- Restrict SNMP access to trusted LAN systems.
- Do not expose UDP 161 to the internet.
- Avoid sharing screenshots containing private SNMP information.

### Build PFLT14 Manually

From PowerShell or Command Prompt:

```bat
cd src\PFLT14
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

### PFLT14 Data and Logs

PFLT14 stores its local data here:

```text
Documents\PFLT14
```

That folder may include settings, logs, debug text, CSV data, and screenshots.

Those files are intentionally ignored by Git and should not be committed unless you have reviewed them.

### PFLT14 Troubleshooting

#### App cannot connect

Check:

- pfSense SNMP service is enabled.
- The pfSense IP address is correct.
- The SNMP community string is correct.
- UDP `161` is allowed from your PC to pfSense.
- You are monitoring the correct pfSense interface.

#### Wrong interface is shown

Open **Settings**, run discovery, and manually select the correct interface/port.

PFLT14 saves that selection for the next launch.

#### Windows blocks the downloaded ZIP

Right-click the ZIP, choose **Properties**, check **Unblock**, click **Apply**, then extract again.

---

# Repository Layout

Current major folders and files include:

```text
Visual-Studio-APPs/
├─ AICreatureLab_v1.0.3/
├─ AtomPlayground_WPF_CSharp_Project_v10_scientifically_grounded/
├─ BioGenesisX_v15b/
├─ DimensionExplorer_CSharp_WinForms_FIXED/
├─ GreatFluidDynamics_Rebuilt/
├─ GreatFluidDynamics_Rebuilt_v2/
├─ HelixSolarShow_Project_v5/
├─ LifeForgeAccelerated v9/
├─ LifeForgeAccelerated_autoevolution_persistence/
├─ LifeForgeAccelerated_gpu_multithreaded_final_fix2/
├─ NewtonsCradleStudio_v3/
├─ docs/
├─ src/
│  └─ PFLT14/
├─ CHANGELOG.md
├─ CONTRIBUTING.md
├─ LICENSE
├─ README.md
└─ run_PFLT14.cmd
```

---

# General Requirements

Most projects in this repository are intended for Windows desktop development.

Recommended tools:

- Windows 10 or Windows 11
- Visual Studio 2022
- .NET 8 SDK
- C# desktop development workload
- A reasonably modern GPU for graphics-heavy projects
- Enough RAM for large simulations

Some projects may have their own requirements inside their project folders.

---

# How To Use This Repository

1. Clone or download the repository.
2. Open the project folder you want.
3. Look for a `.sln`, `.csproj`, or run script.
4. Restore dependencies if needed.
5. Build in Visual Studio or with `dotnet build`.
6. Run the app and experiment.

Example command-line build:

```bat
dotnet restore
dotnet build
```

Some projects may need to be opened directly in Visual Studio.

---

# Repository Goals

This repository exists to:

- help people learn
- provide practical examples
- share useful tools
- encourage experimentation
- support home-lab communities
- create understandable open projects
- make visual software fun
- show how C# can be used for simulations, dashboards, and graphics

---

# Safety And Security Notice

Some projects may interact with:

- routers
- SNMP services
- automation systems
- local network devices
- simulation engines
- hardware experiments

Always review configurations before exposing systems to public networks.

Never publish private credentials, keys, SNMP community strings, router information, or sensitive network details.

---

# Releases

Releases may include:

- source code
- ZIP packages
- build scripts
- documentation
- setup instructions
- screenshots
- revision notes

---

# Planned Future Improvements

Possible future improvements across the repository:

- More screenshots for each project
- Better per-project README files
- More build-and-run scripts
- Cleaner release ZIP packages
- More telemetry and monitoring tools
- More physics and science visualizations
- More AI and artificial-life simulations
- Better GPU/multithread support where practical

---

# Notes

These are hobby, learning, science, graphics, AI, and home-lab projects.

They are meant to be useful, fun, understandable, and easy to experiment with.

PFLT14 is stable and tested in long-running home-lab environments.

---

# License

See the `LICENSE` file for licensing details.

---

# Thank You

If these projects help you learn, experiment, or build something interesting, that means the repository succeeded. :)
