# Client 구조

정적 서버만으로 실행되는 ES module 구조입니다. 별도 번들러 없이 `client/index.html`에서 `src/main.js`를 진입점으로 로드합니다.

- `main.js`: 브라우저 진입점
- `app/bootstrap.js`: 데이터 로딩 후 게임/전투 런타임 조립
- `data/gameData.js`: JSON 데이터 로더
- `game/createGameRuntime.js`: 필드 맵, 입력, 대화, 저장, 멀티플레이 메뉴 런타임
- `battle/createBattleRuntime.js`: 턴제 전투 런타임

새 기능을 추가할 때는 `bootstrap.js`에서 런타임 의존성을 명시적으로 주입하고, 전역 변수 추가는 피합니다.
