# HANDOFF

## 로컬 실행 방법

### 1. PostgreSQL 실행

Docker Desktop이 켜져 있으면 아래 명령으로 개발용 DB를 실행한다.

```bash
cd /Users/emfpdlzj/Desktop/NxtCloud-pokemon2
docker compose up -d postgres
```

Docker를 쓰지 않고 로컬 PostgreSQL을 직접 쓸 수도 있다. 이 경우 아래 DB가 있어야 한다.

```text
Host=localhost
Port=5432
Database=pokemon2
Username=postgres
Password=postgres
```

DB가 없으면 생성한다.

```bash
psql -h localhost -U postgres -d postgres -c 'CREATE DATABASE pokemon2;'
```

### 2. 백엔드 서버 실행

```bash
cd /Users/emfpdlzj/Desktop/NxtCloud-pokemon2
dotnet run --project server/Pokemon2.Server/Pokemon2.Server.csproj
```

기본 주소는 아래와 같다.

```text
http://localhost:5140
```

서버가 처음 실행될 때 PostgreSQL에 `player_saves` 테이블을 자동 생성한다.

확인:

```bash
curl -s http://localhost:5140/api/health
curl -s http://localhost:5140/api/rooms
curl -s 'http://localhost:5140/api/saves?mode=single'
```

### 3. 프론트엔드 실행

새 터미널에서 정적 서버를 실행한다.

```bash
cd /Users/emfpdlzj/Desktop/NxtCloud-pokemon2/pokemon-demo
python3 -m http.server 8000
```

브라우저에서 접속한다.

```text
http://localhost:8000
```

메인 메뉴에서:

- `싱글 모드`: 새 싱글 게임 시작
- `세이브 불러오기`: PostgreSQL 세이브 슬롯 조회
- `멀티 모드`: 서버 방 목록 조회 / 방 생성
- `설정`: 백엔드 서버 주소 변경

### 4. 종료

프론트/백엔드 서버는 실행 중인 터미널에서 `Ctrl+C`로 종료한다.

Docker PostgreSQL을 사용했다면 필요할 때 종료한다.

```bash
docker compose down
```

DB 데이터까지 지우려면 볼륨도 같이 삭제한다.

```bash
docker compose down -v
```

## 자주 확인할 점

- 백엔드 서버 주소는 기본 `http://localhost:5140`이다.
- 프론트는 기본적으로 이 주소로 API를 호출한다.
- PostgreSQL이 꺼져 있으면 세이브 API는 실패한다.
- 프론트는 서버 저장 실패 시 브라우저 `localStorage` fallback을 사용한다.
- Docker 실행 시 `Cannot connect to the Docker daemon`이 나오면 Docker Desktop을 먼저 켠다.

## 검증 명령

```bash
dotnet build server/Pokemon2.Server/Pokemon2.Server.csproj
node --check pokemon-demo/game.js
node --check pokemon-demo/battle.js
docker compose config
```
