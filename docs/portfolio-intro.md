# Portfolio Intro

실제 저장소 구현을 기준으로 정리한 제출용 소개문이다. 현재 저장소에 없는 `Kotlin`, `FastAPI`, `UnityAR`, `STT` 같은 기술은 포함하지 않는다.

## 짧은 소개문

`Pokemon2 Online`은 기존 포켓몬 스타일 싱글플레이 웹 RPG 데모를 온라인 멀티플레이 구조로 확장한 프로젝트입니다. 프런트엔드는 HTML/CSS/JavaScript와 Canvas 기반으로 필드 탐험, NPC 대화, 턴제 전투를 구성했고, 백엔드는 C#/.NET 10 기반 ASP.NET Core 서버로 방 생성, WebSocket 실시간 동기화, PostgreSQL 세이브 슬롯, 서버 권위형 이동·전투 판정을 구현했습니다. 단순 게임 구현에 그치지 않고 Room Actor 구조, 운영 메트릭, 부하 테스트, 저장 실패 fallback과 같은 실서비스 관점의 문제까지 다룬 포트폴리오입니다.

## 상세 소개문

이 프로젝트는 원래 정적 웹 게임 데모였던 포켓몬 스타일 RPG를, 2~4인 동시 플레이를 가정한 온라인 구조로 리팩토링한 작업입니다.  
클라이언트에서는 Canvas 기반 필드 렌더링, 맵 이동, NPC 이벤트, 라이벌 대화, 턴제 전투, 싱글/멀티 저장 UI를 구현했습니다.  
서버에서는 ASP.NET Core Minimal API와 WebSocket을 사용해 방 목록/생성/입장, 실시간 스냅샷 동기화, 서버 권위형 이동 검증, 서버 권위형 전투 판정, PostgreSQL 세이브 슬롯 API를 구성했습니다.  
추가로 운영 관점에서 admin metrics 보호 정책, migration 기반 DB 적용, 저장 실패 시 localStorage fallback, 부하 테스트용 C# 봇과 Artillery 시나리오까지 포함해 구조를 정리했습니다.

## 기술 스택

- Frontend: HTML, CSS, JavaScript, Canvas 2D, ES Modules
- Backend: C#, .NET 10, ASP.NET Core Minimal API, WebSocket
- Data: PostgreSQL, EF Core, Npgsql
- Architecture: Room Actor, Command Queue, 20Hz Tick Loop, 서버 권위형 판정
- Testing: Node.js test runner, xUnit
- Load Test / Ops: C# Load Test Client, Artillery, Datadog DogStatsD integration structure

## 강조할 포인트

- 기존 싱글플레이 데모를 온라인 멀티플레이 구조로 확장했다.
- 클라이언트와 서버를 분리하고, 이동/전투를 서버 권위형으로 재설계했다.
- 세이브 슬롯, 재시도, local fallback, 충돌 우선순위처럼 데이터 유실 관점의 UX를 보강했다.
- 운영 메트릭, 인증 기반 보호, migration, 부하 테스트까지 포함해 실서비스형 백엔드 포트폴리오로 정리했다.
