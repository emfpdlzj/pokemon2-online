# **🎮 Pokemon2 - AI 기반 포켓몬 스타일 RPG**

---

## **🔗 Demo**

👉 http://ajuuniv-06-kiro.s3-website-us-east-1.amazonaws.com/

---

## **🧩 프로젝트 소개**

**Pokemon2**은 포켓몬 시리즈의 초반 게임 흐름을 기반으로 한 웹 게임 데모입니다.

플레이어는 스타팅 몬스터를 선택하고, 마을과 필드를 탐험하며 전투를 진행하고,

특히 라이벌 캐릭터와의 대화를 통해 **동적인 상호작용 경험**을 제공합니다.

---

넥스트클라우드 2026 AJOU AWS 프로그램에서 Kiro와 CodeX를 통해 제작해봤습니다.

---

## **✨ 주요 기능**

### **🗺️ 필드 탐험 시스템**

- 타일 기반 맵 이동
- 마을 / 도로 / 건물 구조 구현
- 맵 간 이동 및 충돌 처리
![](./image/스크린샷%202026-03-28%2015.50.11.png)
![](./image/스크린샷%202026-03-28%2015.50.34.png)

---

### **🧍 NPC 상호작용**

- NPC와 대화 시스템
- 이벤트 기반 트리거 (라이벌 등장 등)
- 선택지 기반 대화 UI
![](./image/스크린샷%202026-03-28%2015.50.54.png)
---

### **🤖 LLM 기반 라이벌 대화**

- 라이벌 캐릭터와 자유 대화 가능
- LLM API 연동
- 게임 세계관 유지하도록 프롬프트 제어
![](./image/스크린샷%202026-03-28%2015.51.17.png)
---

### **⚔️ 턴제 전투 시스템**

- 플레이어 vs 적 몬스터
- 경험치 & 레벨업 시스템
- 공격 / 스킬 / 도망 기능
- 전투 로그 출력
![](./image/스크린샷%202026-03-28%2015.50.38.png)
---

### **🏠 마을 시스템**

- 시작 마을
- 나팔꽃마을 (2번째 마을)
- 포켓몬센터 (회복 기능)
![](./image/스크린샷%202026-03-28%2015.51.07.png)
![](./image/스크린샷%202026-03-28%2015.51.17.png)
---

### **💾 상태 저장**

- PostgreSQL 기반 서버 세이브 슬롯 API
- 싱글 / 멀티 모드 저장 데이터 분리
- 3개 세이브 슬롯
- localStorage fallback으로 브라우저 진행 상태 유지
- 맵 위치 / 레벨 / HP / 이벤트 상태 유지

---

## **🛠️ 기술 스택**

- **Frontend**: HTML, CSS, JavaScript (Vanilla)
- **Rendering**: Canvas 기반 2D 렌더링
- **Storage**: localStorage
- **Database**: PostgreSQL / EF Core / Npgsql
- **AI**: LLM API (라이벌 대화)
- **Deployment**: AWS S3 정적 호스팅
- **Online Server**: C# / ASP.NET Core / WebSocket / Room Actor

---

## **🖥️ 온라인 서버 포트폴리오**

이 저장소는 2~4인 동시 플레이 서버 포트폴리오로 확장 중입니다.

- 서버 권위형 이동 판정
- Room Actor 기반 방 단위 command queue
- WebSocket 실시간 동기화
- 20Hz tick loop
- 동시 이동 충돌 거부
- 부하 테스트용 C# 봇 클라이언트
- 운영 메트릭 API
- PostgreSQL 세이브 슬롯 API

자세한 설계와 트러블슈팅 주제는 [docs/server-portfolio.md](./docs/server-portfolio.md)를 참고하세요.

---

## **🚀 실행 방법**

### **1. 로컬 실행**

```
# 프로젝트 폴더 이동
cd pokemon-demo

# 간단 서버 실행 (예: python)
python3 -m http.server
```

👉 http://localhost:8000 접속
![]

### **2. 온라인 서버 + PostgreSQL 실행**

```bash
# 개발용 PostgreSQL 실행
docker compose up -d postgres

# ASP.NET Core 서버 실행
dotnet run --project server/Pokemon2.Server/Pokemon2.Server.csproj
```

기본 서버 주소는 `http://localhost:5140`입니다. 메인 메뉴의 설정에서 서버 주소를 바꿀 수 있습니다.

세이브 API:

```http
POST /api/saves
GET /api/saves?mode=single
GET /api/saves/{saveId}
PUT /api/saves/{saveId}
DELETE /api/saves/{saveId}
```

---

### **3. 배포 (S3)**

```
aws s3 sync . s3://<your-bucket-name>
```

---

## **⚠️ 주의사항**

- LLM API 키는 클라이언트에 포함되어 있으므로 **개인 테스트용으로만 사용**
- 실제 서비스 시에는 반드시 **백엔드 서버를 통해 API 호출 필요**

---


## **👩‍💻 Author**

**박민정**

아주대학교 소프트웨어학과

---
