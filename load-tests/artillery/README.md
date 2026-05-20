# Artillery WebSocket Load Tests

Artillery is used here for reproducible WebSocket load reports. The existing C# `Pokemon2.LoadTest` project remains the game-specific verifier for collision, sequence, and per-client RTT details.

## Install

```bash
npm install
```

## Run The Server

```bash
dotnet run --project server/Pokemon2.Server/Pokemon2.Server.csproj --urls http://localhost:5199
```

If the server is running on another port, set both targets:

```bash
export POKEMON2_HTTP_TARGET=http://localhost:5199
export POKEMON2_WS_TARGET=ws://localhost:5199
```

## Room Scale Test

Creates one room per virtual user, connects over WebSocket, and sends movement packets at mixed fast/normal/slow client paces.

```bash
npm run load:artillery:rooms
```

Use this for room actor count, WebSocket message throughput, and command queue latency from `GET /api/admin/metrics`.

## Hot Room Test

Connects four virtual users to one room and sends movement packets with mixed client paces. The room capacity is four, so this scenario intentionally caps arrivals at four.

```bash
npm run load:artillery:hot-room
```

To target a specific room:

```bash
export POKEMON2_ROOM_ID=room-abc12345
npm run load:artillery:hot-room
```

Use this as a quick open-source smoke test for shared-room WebSocket traffic. For collision result counts and observed RTT, run the C# load test because it parses `player_moved` and `move_rejected` payloads directly.
