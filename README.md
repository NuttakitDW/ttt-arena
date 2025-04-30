# Tic‑Tac‑Toe WebSocket PoC

A one‑evening exercise to understand **WebSockets + SignalR**. Two files—one server, one client—show a complete, real‑time turn‑based game.

---

## Why I built this
* **Goal**  → see the full request/response flow of a WebSocket app.
* **Things I wanted to learn**
  1. SignalR handshake & upgrade process
  2. How a client method (`hub.invoke`) maps to a C# hub method
  3. How the server broadcasts back to all players
* **What I learned**
  * JSON frames (`type: 1`, `type: 6`) and why pings matter
  * Each browser has its own hub instance; `Clients.Group` fans messages out
  * One ping every 15 s keeps reverse proxies from killing idle sockets

---

## Run it
```bash
# prerequisites: .NET 8 SDK + a browser
cd Server
# bind to any free port (5000 is easy to remember)
dotnet run --urls http://localhost:5000
```
Open **two** tabs at <http://localhost:5000>—first tab is **X**, second is **O**.
Click alternately and watch the marks appear in both tabs instantly.

---

## File map
```
Server/
├ Program.cs   ← 80 lines: hub + in‑memory game logic
└ wwwroot/
   └ index.html ← tiny client that calls hub.invoke("Move", idx)
```
That’s it—no database, no controllers, no build scripts.

Enjoy poking around or replacing Tic‑Tac‑Toe with your own turn‑based idea! 😊

