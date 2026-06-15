# Operations And Deployment

운영 배포 전에 필요한 비밀값, 접근 제어, DB 적용 순서를 정리한다.

## Required Secrets

- `POKEMON2_DATABASE_URL` 또는 `ConnectionStrings__DefaultConnection`
- `POKEMON2_PLAYER_IDENTITY_SECRET`
  - 운영 환경에서는 필수다.
  - 클라이언트 `X-Player-Identity`와 WebSocket `playerToken` 검증에 사용한다.
- `POKEMON2_ADMIN_TOKEN`
  - `/api/admin/metrics`를 외부에서 조회할 때 사용한다.
- `POKEMON2_LLM_API_KEY`
  - LLM 기능을 켜는 경우 필요하다.

## Optional Ops Settings

- `POKEMON2_ADMIN_ALLOWLIST`
  - 쉼표로 구분한 IP 목록.
  - 예: `10.0.0.10,10.0.0.11`
- `AdminApi__AllowLoopbackInDevelopment`
  - 기본값은 Development에서 `true`.
  - 로컬 운영 패널과 개발용 확인을 쉽게 하려는 설정이다.

## Admin Metrics Policy

- `/api/admin/metrics`는 다음 중 하나를 만족해야 접근 가능하다.
- `X-Admin-Token` 헤더가 `POKEMON2_ADMIN_TOKEN`과 일치한다.
- 요청 IP가 `POKEMON2_ADMIN_ALLOWLIST`에 포함된다.
- Development 환경에서 루프백 주소로 접근하고 `AdminApi__AllowLoopbackInDevelopment=true`다.

브라우저 운영 패널을 사용할 때는 클라이언트 설정 화면의 관리자 토큰 입력칸에 같은 값을 저장한다.

## Database Apply Order

1. PostgreSQL 연결 문자열을 설정한다.
2. 운영 환경이면 `POKEMON2_PLAYER_IDENTITY_SECRET`를 먼저 설정한다.
3. 새 배포본을 실행한다.
4. 서버 시작 시 기존 레거시 테이블을 baseline한 뒤 `MigrateAsync()`가 migration 이력을 맞춘다.
5. `__EFMigrationsHistory`에 `20260615170000_InitialSchema`가 기록됐는지 확인한다.
6. `/api/health`, 저장 API, 멀티 방 입장 흐름을 확인한다.

주의:

- 기존 공용 세이브 데이터는 baseline 과정에서 `legacy-default` 사용자로 채워진다.
- 운영 DB에서는 앱 시작 전에 별도 `EnsureCreated`를 호출하지 않는다.

## Load Test And Ops Tools

### Artillery

- `POKEMON2_HTTP_TARGET`만 맞으면 테스트가 `identity -> join -> wsUrl` 순서로 접속한다.
- 운영 지표까지 보려면 `POKEMON2_ADMIN_TOKEN`도 함께 설정한다.

### C# Load Test

- `dotnet run --project server/Pokemon2.LoadTest/Pokemon2.LoadTest.csproj`
- 필요 시 `--http=http://host:port --adminToken=...`를 넘긴다.
- 각 봇은 `/api/player/identity`로 토큰을 발급받고 `/api/rooms/{roomId}/join` 응답의 `wsUrl`로 접속한다.
