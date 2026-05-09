# PFLT14 — pfSense Live Telemetry Rev 14

PFLT14 is a full-screen Windows dashboard for watching a pfSense router/firewall in real time using SNMP. It was built to be simple for a home lab: no SSH scraping, no pfSense web login, and no paid monitoring stack required.

The dashboard shows live traffic, selected-interface stats, link speed, byte counters, latency/probe health, retry status, peak values, and multiple graph-style cards in one clean screen.

## What it does

- Reads pfSense interface counters through SNMP v2c.
- Lets you choose the monitored interface/port from the dashboard.
- Saves the selected port and settings under `Documents\PFLT14`.
- Auto-scales traffic display from Mbps to Gbps/Tbps when needed.
- Auto-scales byte counters from Bytes to KBytes/MBytes/GBytes/TBytes.
- Handles counter resets, 32-bit rollover, and impossible one-sample spikes.
- Corrects common bad 5G link-speed reporting where some interfaces report `50 Gbps` instead of `5 Gbps`.
- Includes reconnect/retry behavior for more stable long-running monitoring.
- Provides debug copy/save, logs, CSV output, screenshots, and a fullscreen/window toggle.

## Requirements

### PC

- Windows 10 or Windows 11.
- .NET 8 SDK, recommended for building from source.
  - Install the **.NET 8 SDK for Windows x64** from Microsoft.
- Network access from the PC to pfSense on UDP port `161`.

### pfSense

- pfSense with SNMP enabled.
- SNMP v2c community string configured.
- Firewall rule allowing the dashboard PC to reach pfSense on UDP `161`.

This program does **not** need pfSense SSH access, web GUI credentials, or admin password storage.

## Quick start

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

Change the community string to match your pfSense setup. Do not publish your real private community string in screenshots, commits, or bug reports.

## Recommended pfSense SNMP setup

In pfSense:

1. Go to **Services → SNMP**.
2. Enable SNMP.
3. Set a community string.
4. Limit access to your trusted LAN when possible.
5. Add or confirm a firewall rule that permits the monitoring PC to reach pfSense on UDP `161`.

For safety, use a unique community string instead of `public`.

## Build manually

From PowerShell or Command Prompt:

```bat
cd src\PFLT14
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

## Project layout

```text
PFLT14_GitHub_Ready/
├─ README.md
├─ LICENSE
├─ CHANGELOG.md
├─ CONTRIBUTING.md
├─ .gitignore
├─ src/
│  └─ PFLT14/
│     ├─ App.xaml
│     ├─ App.xaml.cs
│     ├─ MainWindow.xaml
│     ├─ MainWindow.xaml.cs
│     ├─ PFLT14.csproj
│     └─ build_and_run.cmd
└─ docs/
   ├─ REV11_NOTES.txt
   ├─ REV12_NOTES.txt
   ├─ REV13_NOTES.txt
   └─ REV14_NOTES.txt
```

## Data and logs

PFLT14 stores its local data here:

```text
Documents\PFLT14
```

That folder may include settings, logs, debug text, CSV data, and screenshots. Those files are intentionally ignored by Git and should not be committed unless you have reviewed them.

## Troubleshooting

### App cannot connect

Check:

- pfSense SNMP service is enabled.
- The pfSense IP address is correct.
- The SNMP community string is correct.
- UDP `161` is allowed from your PC to pfSense.
- You are monitoring the correct pfSense interface.

### Wrong interface is shown

Open **Settings**, run discovery, and manually select the correct interface/port. PFLT14 saves that selection for the next launch.

### Windows blocks the downloaded ZIP

Right-click the ZIP, choose **Properties**, check **Unblock**, click **Apply**, then extract again.

## Notes

This is a helpful home-lab telemetry dashboard. It is not a replacement for enterprise monitoring systems, but it is great for learning, visual checking, and watching a pfSense router live on a dedicated screen.


# Home Lab Projects Repository

A collection of open-source hardware, software, networking, FPGA, AI, telemetry, and homelab projects focused on learning, experimentation, visualization, and practical real-world systems.

This repository contains projects ranging from FPGA development and AI simulations to pfSense telemetry dashboards, cluster computing, SDR tools, Minecraft AI systems, and embedded hardware development.

---

# Featured Projects

---

# PFLT14 — pfSense Live Telemetry Rev 14

PFLT14 is a full-screen Windows dashboard for watching a pfSense router/firewall in real time using SNMP.

It was designed for home labs and dedicated monitoring screens where users want clean, live telemetry without requiring enterprise monitoring stacks, browser dashboards, SSH scraping, or cloud services.

## Features

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

## What it does

- Reads pfSense interface counters through SNMP v2c
- Lets users choose monitored interfaces directly from the dashboard
- Saves settings locally under:

```text
Documents\PFLT14
```

- Corrects common 5G reporting issues where interfaces incorrectly report `50 Gbps`
- Handles counter rollovers and impossible spike values
- Provides fullscreen and windowed operation modes

## Requirements

### PC

- Windows 10 or Windows 11
- .NET 8 SDK for building from source
- Network access to pfSense over UDP 161

### pfSense

- SNMP enabled
- SNMP v2c community configured
- Firewall rule permitting UDP 161 access from the monitoring PC

## Quick Start

1. Open:

```text
src\PFLT14
```

2. Run:

```text
build_and_run.cmd
```

3. Open Settings inside the app
4. Enter pfSense IP and SNMP community
5. Run interface discovery
6. Select the correct monitored interface

## Recommended SNMP Security

- Use a custom community string
- Restrict SNMP to trusted LAN devices
- Never expose UDP 161 publicly
- Avoid sharing screenshots with private SNMP data visible

## Why This Exists

PFLT14 was built to provide a lightweight, visually clean telemetry dashboard for pfSense users who want:

- simple setup
- real-time visibility
- fullscreen monitoring
- no cloud dependency
- no browser dashboards
- no SSH scraping

The goal is practical live telemetry for real home labs.

## Status

Stable and tested in long-running home-lab environments.

---

# Additional Projects

This repository may also include:

- FPGA development projects
- Vivado and Verilog examples
- SDR tools and utilities
- AI simulations and autonomous systems
- Cluster-computing experiments
- Minecraft AI and automation systems
- Embedded-system development
- Retro-computing projects
- Visualization dashboards
- Network-monitoring tools
- Scientific simulations

---

# Repository Goals

This repository exists to:

- help people learn
- provide practical examples
- share useful tools
- encourage experimentation
- support home-lab communities
- create understandable open projects

---

# Security Notice

Some projects may interact with:

- routers
- SDR hardware
- SNMP services
- automation systems
- cluster nodes
- embedded hardware

Always review configurations before exposing systems to public networks.

Never publish private credentials, keys, SNMP community strings, or sensitive network details.

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

- Expanded telemetry systems
- Additional SNMP support
- Improved graphing
- Multi-device dashboards
- GPU-assisted visualizations
- AI-assisted monitoring ideas
- Better long-term logging systems
- Additional FPGA and embedded projects

---

# License

See the LICENSE file for licensing details.

---

# Thank You

If these projects help you learn, experiment, or build something interesting, that means the repository succeeded. :)

