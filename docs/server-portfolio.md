# Pokemon2 Online Server Portfolio

## 목표

기존 싱글플레이 웹 데모를 2~4인 동시 플레이가 가능한 서버 권위형 온라인 RPG로 확장한다. 이 서버는 채용 공고의 `Web Server`, `Socket Server`, `게임 서버 기술 R&D`, `라이브 서비스 운영 툴` 역량을 보여주기 위한 백엔드 포트폴리오다.

## 기술 스택

- C# / .NET 10
- ASP.NET Core Minimal API
- WebSocket
- PostgreSQL
- EF Core / Npgsql
- Room Actor + Command Queue
- 20Hz Tick Loop
- 서버 권위형 이동 판정
- 부하 테스트용 C# WebSocket 봇 클라이언트

## 서버 구조

```text
Browser Client
  |
  | REST: 방 생성, 방 목록, 세이브 슬롯, 헬스 체크, 운영 메트릭
  | WebSocket: 이동, 채팅, 스냅샷 동기화
  v
ASP.NET Core Gateway
  |
  +--> PostgreSQL
  |    - player_saves
  |    - single / multi mode
  |    - slot 1~3
  |
  v
RoomManager
  |
  v
RoomActor per room
  - single-reader command queue
  - 20Hz tick loop
  - collision validation
  - movement rate limit
  - snapshot broadcast
  - room-level metrics
```

## 주요 API

```http
GET /api/health
POST /api/sessions/single
GET /api/rooms
POST /api/rooms
POST /api/rooms/{roomId}/join
GET /api/saves?mode={single|multi}
POST /api/saves
GET /api/saves/{saveId}
PUT /api/saves/{saveId}
DELETE /api/saves/{saveId}
GET /api/admin/metrics
GET /ws/game?roomId={roomId}&playerName={name}
```

## 세이브 데이터 모델

```text
player_saves
- id
- slot_number
- mode: single | multi
- player_name
- current_map
- position_x / position_y
- starter_id / starter_name / starter_level / starter_current_hp
- play_time_seconds
- events_json
- game_state_json
- created_at / updated_at
```

싱글 저장은 개인 진행도를 저장하고, 멀티 저장은 캐릭터 상태를 방 진행도와 분리해 저장한다. 프론트엔드는 서버 세이브 실패 시 localStorage fallback을 사용한다.

## WebSocket 클라이언트 패킷

```json
{
  "type": "move",
  "direction": "Right",
  "sequence": 1
}
```

```json
{
  "type": "chat",
  "message": "안녕"
}
```

## WebSocket 서버 패킷

```json
{
  "type": "snapshot",
  "payload": {
    "roomId": "room-12345678",
    "serverTick": 10,
    "serverTimeMs": 1779255158123,
    "players": []
  }
}
```

```json
{
  "type": "move_rejected",
  "payload": {
    "sequence": 3,
    "reason": "blocked",
    "position": { "x": 9, "y": 9 }
  }
}
```

## 트러블슈팅 설계

### 1. 동시 이동 충돌

문제:
2명 이상의 플레이어가 같은 타일로 동시에 이동하면 클라이언트 기준 처리에서는 같은 위치를 점유하는 불일치가 생긴다.

해결:
`RoomActor`가 단일 command queue에서 `Move` 요청을 순서대로 처리한다. 서버가 맵 충돌과 플레이어 점유 타일을 검증한 뒤 승인 또는 거부한다.

관측 지표:
- `acceptedMoves`
- `rejectedMoves`
- `move_rejected.reason = blocked`

### 2. 느린 클라이언트와 빠른 클라이언트 동기화

문제:
RTT가 다른 클라이언트는 서로 다른 시점의 위치를 보게 되고, 즉시 위치 확정을 클라이언트가 하면 보정이 어렵다.

해결:
서버가 `serverTick`, `serverTimeMs`, `sequence`를 포함한 snapshot과 이동 결과를 브로드캐스트한다. 클라이언트는 이를 기준으로 interpolation 또는 reconciliation을 붙일 수 있다.

관측 지표:
- 부하 테스트 클라이언트의 `avgObservedRttMs`
- snapshot 수
- sequence별 승인/거부 응답 시간

### 3. 방 서버 부하

문제:
모든 방을 하나의 루프에서 처리하면 특정 방의 부하가 전체 서버 지연으로 번질 수 있다.

해결:
방마다 독립적인 `RoomActor`와 tick loop를 둔다. 방 단위 queue latency를 측정해 부하가 몰리는 방을 식별한다.

관측 지표:
- room count
- player count
- total ticks
- averageCommandLatencyMs

## 실행

```bash
docker compose up -d postgres
dotnet build server/Pokemon2.Server/Pokemon2.Server.csproj
dotnet server/Pokemon2.Server/bin/Debug/net10.0/Pokemon2.Server.dll --urls http://localhost:5199
```

## 검증

```bash
curl -s http://localhost:5199/api/health
```

```bash
curl -s -X POST http://localhost:5199/api/saves \
  -H 'Content-Type: application/json' \
  -d '{"slotNumber":1,"mode":"single","playerName":"주인공","currentMap":"hometown","positionX":9,"positionY":9,"starter":null,"events":{},"gameState":{"currentMap":"hometown","px":9,"py":9},"playTimeSeconds":0}'
```

```bash
dotnet run --project server/Pokemon2.LoadTest/Pokemon2.LoadTest.csproj -- \
  --http=http://localhost:5199 \
  --ws=ws://localhost:5199 \
  --rooms=2 \
  --clients=2 \
  --moves=5 \
  --moveIntervalMs=120 \
  --delayMs=20
```

검증 예시:

```text
acceptedMoves=32
rejectedMoves=4
snapshots=32
avgObservedRttMs=1.45
averageCommandLatencyMs=0.025
```

## 다음 구현 후보

- 기존 `pokemon-demo` Canvas 클라이언트에 WebSocket 실시간 위치 어댑터 연결
- LLM 대화 API 서버 프록시화
- 전투 상태 머신 서버화
- DB 저장 확장: 계정, 캐릭터, 방 기록
- 운영툴 UI: 방 목록, 접속자, 강제 퇴장, 메트릭 그래프
- 부하 테스트 결과를 CSV로 저장하고 그래프화
