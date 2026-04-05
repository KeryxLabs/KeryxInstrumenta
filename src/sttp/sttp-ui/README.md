# sttp-ui

Simple mobile-first Blazor UI for `sttp-gateway`.

This UI is intended for trusted environments (local network / VPN) and does not include built-in auth in the MVP.

## Configure gateway target

Set `Gateway:BaseUrl` in:

- `appsettings.json`
- `appsettings.Development.json`

Default:

```json
{
  "Gateway": {
    "BaseUrl": "http://127.0.0.1:8080"
  }
}
```

## Run

```bash
cd src/sttp/sttp-ui
dotnet run
```

To bind on all interfaces for phone access over LAN/VPN:

```bash
cd src/sttp/sttp-ui
dotnet run --no-launch-profile --urls http://0.0.0.0:8090
```

Then open `http://<host-lan-or-vpn-ip>:8090` from your phone.

## What the MVP includes

- Gateway status check (`/health`)
- Quick store form (`/api/v1/store`)
- Session calibration form (`/api/v1/calibrate`)
- Recent nodes list (`/api/v1/nodes`)

## Local network + VPN note

For your OpenVPN setup, host this app on your LAN/VPN-reachable machine and keep gateway base URL reachable from that host. The browser only talks to `sttp-ui`; `sttp-ui` calls the gateway server-side.
