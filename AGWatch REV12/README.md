# AGWatch REV12

**AGWatch REV12** is a Windows desktop dashboard for watching an **AdGuard Home** server in real time.

It is built for a home network where you want fast, easy visibility into DNS activity without running a full monitoring stack. It shows live query load, blocked-query rate, protection status, latency, top clients, top blocked domains, and a recent query log in a clean full-screen dashboard.

REV12 keeps the short app name, starts maximized, saves your settings, encrypts the saved AdGuard password with Windows DPAPI, and adds a dynamic live block-rate history graph.

---

## What it looks at

AGWatch connects to your AdGuard Home web/API address using your AdGuard username and password, then polls these API areas:

- `/control/status`
- `/control/stats`
- `/control/querylog`

It does **not** change AdGuard settings. It is a read-only monitoring dashboard.

---

## Main features

- Live AdGuard Home dashboard for Windows.
- Starts maximized for a clean control-room style view.
- One-button **FULLSCREEN / WINDOW** toggle.
- Auto-start polling option.
- Adjustable polling interval.
- Adjustable recent query log row count.
- Total DNS query count.
- Total blocked count.
- Overall block-rate card.
- Live per-poll block-rate history.
- Dynamic block-rate graph scaling.
- Query-per-second history graph.
- Q/S load meter.
- Block/S meter.
- Top clients panel.
- Top blocked / filtered domains panel.
- Recent query log table.
- Latency estimate from recent query log samples.
- Protection status card.
- Last-update clock.
- Opens the AdGuard web query log from inside the app.
- Saves settings under the current Windows user profile.
- Saves the AdGuard password encrypted, not plain text.

---

## REV12 highlights

REV12 is the current GitHub-ready release.

### Dynamic block-rate history

Older versions could make the block-rate graph look almost flat when the block percentage was low. REV12 fixes that by tracking the **live per-poll block rate** and scaling the graph dynamically around recent values.

The graph now shows:

- Live block percentage
- Average block percentage
- Overall block percentage
- Low recent value
- Peak recent value
- Dynamic scale labels

This makes a normal 3%–10% block rate much easier to see without losing the ability to show spikes.

### Better blocked-domain panel

REV12 keeps the improved blocked-domain logic from the previous revision. It prefers AdGuard's real stats data when available, then falls back to the query log sample when needed.

### Build fix included

REV12 includes the fix for the local variable name conflict in the graph scaling code.

---

## Password and settings safety

AGWatch saves settings here:

```text
Documents\AGWatch\settings.json
```

The AdGuard password is saved as `EncryptedPassword` using Windows DPAPI.

That means:

- The real password is not written to the settings file in plain text.
- The encrypted password is tied to your Windows user account.
- Another Windows account or another PC should not be able to decrypt it.
- If you move the settings file to another PC, re-enter the AdGuard password once.

Legacy plain-text `Password` values from older test builds are imported one time, then cleared when settings are saved again.

---

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK for building from source
- AdGuard Home reachable from the Windows PC
- AdGuard Home username and password

This project targets:

```text
net8.0-windows
Windows Forms
```

---

## Quick start

1. Download or clone this repository.
2. Open the folder in Windows.
3. Run:

```cmd
run.cmd
```

The script will:

1. Clean the Release build.
2. Build the app.
3. Start `AGWatch_REV12.exe`.

When AGWatch opens, enter:

```text
Server: http://YOUR_ADGUARD_IP
User:   your AdGuard username
Pass:   your AdGuard password
```

Then click **START**.

Example server value:

```text
http://192.168.1.206
```

Use your own AdGuard Home IP address.

---

## Debug run

If the normal run does not start, use:

```cmd
run_debug.cmd
```

That creates these files in the project folder:

```text
build_log_REV12.txt
run_log_REV12.txt
```

The app can also write this crash log:

```text
Documents\AGWatch\fatal_error_REV12.txt
```

Those three files are the best files to check when troubleshooting.

---

## Reset saved settings

To clear saved server/user/password settings, run:

```cmd
reset_settings.cmd
```

That backs up and deletes:

```text
Documents\AGWatch\settings.json
```

A backup is saved as:

```text
Documents\AGWatch\settings.json.backup
```

---

## Build manually

From the repository root:

```cmd
dotnet build AGWatch\AGWatch.csproj -c Release
```

The compiled EXE will be here:

```text
AGWatch\bin\Release\net8.0-windows\AGWatch_REV12.exe
```

---

## Publish a local release build

A helper script is included:

```cmd
publish_win_x64.cmd
```

It publishes a local Windows x64 release build to:

```text
publish\win-x64
```

This is useful when you want a cleaner folder to copy to another Windows machine.

---

## Project layout

```text
AGWatch_REV12/
├─ AGWatch/
│  ├─ AGWatch.csproj
│  ├─ Program.cs
│  ├─ MainForm.cs
│  ├─ DashboardControls.cs
│  ├─ AdGuardModels.cs
│  ├─ PasswordVault.cs
│  ├─ app.ico
│  └─ app.png
├─ run.cmd
├─ run_debug.cmd
├─ reset_settings.cmd
├─ publish_win_x64.cmd
├─ README.md
├─ CHANGELOG.md
├─ SECURITY.md
├─ VERSION.txt
└─ .gitignore
```

---

## Notes for GitHub

Recommended repository description:

```text
AGWatch REV12 — Windows dashboard for live AdGuard Home DNS monitoring.
```

Suggested topics:

```text
adguard-home, dns, windows-forms, dotnet, homelab, monitoring, dashboard
```

Do not upload your personal `settings.json`, build output folders, debug logs, or screenshots that show private IPs unless you intentionally want them public.

---

## Revision history summary

- **REV9** — Short-name AGWatch release with encrypted password saving.
- **REV10** — More useful block-rate and top blocked-domain panels.
- **REV11** — Dynamic live block-rate graph logic.
- **REV12** — Build fix, GitHub-ready cleanup, and final dynamic block-rate version.

---

## License

No license file is included by default. Add the license you want before publishing publicly if you want others to have clear reuse rights.
