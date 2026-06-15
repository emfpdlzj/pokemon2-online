# Datadog Observability

Pokemon2 server can publish operational metrics to Datadog through DogStatsD.

## Local Agent

Set your Datadog API key and start the Agent profile:

```bash
export DD_API_KEY=...
export DD_SITE=datadoghq.com
docker compose --profile observability up -d datadog
```

Enable the server exporter:

```bash
export POKEMON2_DOGSTATSD_ENABLED=true
export POKEMON2_DOGSTATSD_HOST=127.0.0.1
export POKEMON2_DOGSTATSD_PORT=8125
export DD_ENV=local
export DD_SERVICE=pokemon2-online-server
dotnet run --project server/Pokemon2.Server/Pokemon2.Server.csproj --urls http://localhost:5199
```

## Metrics

Global metrics:

- `pokemon2.room.count`
- `pokemon2.player.count`
- `pokemon2.battle.active`
- `pokemon2.moves.accepted`
- `pokemon2.moves.rejected`
- `pokemon2.moves.rejected.by_reason` tagged by `reason`
- `pokemon2.command.latency.avg_ms`
- `pokemon2.command.latency.max_ms`
- `pokemon2.tick.delay.avg_ms`
- `pokemon2.tick.delay.max_ms`
- `pokemon2.llm.reply.requests`
- `pokemon2.llm.reply.success`
- `pokemon2.llm.reply.fallback`
- `pokemon2.llm.choices.requests`
- `pokemon2.llm.choices.success`
- `pokemon2.llm.choices.fallback`
- `pokemon2.llm.failures` tagged by `reason`
- `pokemon2.llm.tokens.prompt_total`
- `pokemon2.llm.tokens.completion_total`
- `pokemon2.llm.tokens.total`
- `pokemon2.llm.cost.estimated_usd_total`

Room-level metrics are tagged by `room_id` and `map_id`:

- `pokemon2.room.player_count`
- `pokemon2.room.monster_count`
- `pokemon2.room.active_battle_count`
- `pokemon2.room.server_tick`
- `pokemon2.room.moves.accepted`
- `pokemon2.room.moves.rejected`
- `pokemon2.room.command.latency.avg_ms`
- `pokemon2.room.command.latency.max_ms`
- `pokemon2.room.tick.delay.avg_ms`
- `pokemon2.room.tick.delay.max_ms`

## Dashboard Panels

Use `docs/datadog-dashboard-template.json` as the starting dashboard definition.

Recommended dashboard panels:

- Room count and player count as toplist/timeseries.
- Accepted vs rejected moves as timeseries.
- Rejected moves split by `reason`.
- Average and max command latency.
- Average and max tick delay.
- Per-room player count and rejected moves split by `room_id`.
- LLM 요청량, fallback 비율, `reason`별 실패, 누적 토큰/예상 비용.

## Load Test Flow

Run either Artillery scenario while the server exporter is enabled:

```bash
npm run load:artillery:hot-room
npm run load:artillery:rooms
```

For domain-specific reject reason checks, run:

```bash
dotnet run --project server/Pokemon2.LoadTest/Pokemon2.LoadTest.csproj -- --scenario=collision
dotnet run --project server/Pokemon2.LoadTest/Pokemon2.LoadTest.csproj -- --scenario=sync-pace
```

Use the Datadog dashboard during these runs to show latency, reject reason distribution, room count, and player count under load.
