# Security Notes

AGWatch REV12 is a read-only dashboard for an AdGuard Home server.

## Password storage

The AdGuard Home password is saved using Windows DPAPI through `PasswordVault.cs`.

Important behavior:

- The settings file stores `EncryptedPassword`, not the plain password.
- The encrypted value is tied to the current Windows user account.
- Moving `settings.json` to a different Windows user or PC should not decrypt the saved password.
- If DPAPI cannot decrypt the password, AGWatch leaves the password blank and you can re-enter it.

## Files not to publish

Do not publish these files to GitHub:

```text
Documents\AGWatch\settings.json
Documents\AGWatch\settings.json.backup
Documents\AGWatch\fatal_error_REV12.txt
build_log_REV12.txt
run_log_REV12.txt
AGWatch\bin\
AGWatch\obj\
publish\
```

## Network access

AGWatch connects to the AdGuard Home API address you enter in the Server box. It does not expose a server or listen for inbound connections.

## Recommended use

Use AGWatch on your trusted LAN. If your AdGuard Home interface is exposed outside your network, secure it first before using any monitoring tool against it.
