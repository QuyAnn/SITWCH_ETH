# SITWCH_ETH – Network Route Manager

A **C# WinForms (.NET 8)** desktop application for managing Windows routing table entries
so that internal networks (`10.x.x.x`) flow through the **Ethernet** interface while the
rest of Internet traffic flows through **WiFi**.

> ⚠️ **Requires Windows + Administrator privileges.**
> The `app.manifest` forces UAC elevation automatically on launch.

---

## Features

| Feature | Detail |
|---|---|
| Add / remove routes | Uses the built-in `route` command via `ProcessStartInfo` |
| CIDR → subnet mask conversion | Pure in-process arithmetic, no external libraries |
| Duplicate protection | Checks routing table before adding, skips existing routes |
| Input validation | Validates gateway IPs and CIDR strings; never crashes on bad input |
| Save / Load config | JSON file (`config.json`) using `System.Text.Json` |
| Log panel | Dark-mode terminal-style log with timestamps |
| UAC elevation | `app.manifest` → `requireAdministrator` |

---

## UI Overview

```
┌─ Gateway Configuration ──────────────────────────────────────────┐
│  Ethernet Gateway: [ 10.21.99.1  ]   WiFi Gateway: [192.168.5.1] │
└──────────────────────────────────────────────────────────────────┘

┌─ Route Entries ──────────────────────────────────────────────────┐
│  Networks (CIDR) → Ethernet  │  Individual IPs → Ethernet        │
│  10.53.0.0/16                │  10.53.118.120                     │
│  10.21.0.0/16                │  ...                               │
└──────────────────────────────────────────────────────────────────┘

[ ▶ Apply Routes ]  [ 💾 Save Config ]  [ 📂 Load Config ]  [ 🗑 Clear Log ]

┌─ Log ────────────────────────────────────────────────────────────┐
│ [14:05:01] === Apply Routes – Started ===                         │
│ [14:05:01]   [OK]   Deleted route 0.0.0.0                        │
│ [14:05:02]   [OK]   route add 10.53.0.0 mask 255.255.0.0 ...     │
└──────────────────────────────────────────────────────────────────┘
```

---

## Config File Format

```json
{
  "ethGateway":  "10.21.99.1",
  "wifiGateway": "192.168.5.1",
  "networks":    ["10.53.0.0/16", "10.21.0.0/16"],
  "ips":         ["10.53.118.120"]
}
```

---

## Building

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
* Windows (WinForms is Windows-only)

### Run in development

```powershell
dotnet run
```

### Build a self-contained single-file EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output EXE will be at:

```
bin\Release\net8.0-windows\win-x64\publish\SITWCH_ETH.exe
```

### Framework-dependent EXE (smaller, requires .NET 8 runtime on target machine)

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

---

## Apply logic (what "Apply Routes" does)

1. **Delete** stale routes: `10.53.0.0`, `10.21.0.0`, `0.0.0.0` (default)
2. **Add** each CIDR network → Ethernet gateway, metric 5
   ```
   route add <network> mask <mask> <ethGateway> metric 5
   ```
3. **Add** each individual IP → Ethernet gateway, metric 5
   ```
   route add <ip> mask 255.255.255.255 <ethGateway> metric 5
   ```
4. **Add** default route → WiFi gateway, metric 1
   ```
   route add 0.0.0.0 mask 0.0.0.0 <wifiGateway> metric 1
   ```

Steps 2–4 skip any route that already exists (duplicate protection).
