---
name: client-testing
description: 브라우저 client 코드의 테스트 작성 및 실행 방법을 안내한다. UI, 정적 ES module, React/Vue/Svelte/Next/Vite, DOM, 브라우저 플로우 테스트를 작성하거나 실행하는 경우 이 스킬을 사용한다.
---

# Client 테스트 가이드

이 가이드는 브라우저 client 테스트를 작성하거나 실행할 때 사용한다. 작업 전에는 저장소의 실제 client 구조, package manager, 테스트 도구, 실행 스크립트를 먼저 확인하고, 확인되지 않은 정보는 추측하지 않는다.

## 공통 원칙

- 부정확한 정보가 있거나 실행 환경이 불명확하면 사용자에게 질문한다.
- 테스트 프레임워크(Vitest, Jest, Playwright, Cypress 등)는 기존 프로젝트가 사용하는 것을 따른다.
- 새 테스트 도구나 dependency를 도입해야 하면 사용자에게 먼저 확인한다.
- 실패한 테스트를 고칠 때는 실패 범위를 먼저 좁힌 뒤 관련 테스트만 재실행하고, 마지막에 가능한 전체 검증을 실행한다.
- 외부 API, 실제 서버, 실제 LLM, 시간, 난수, localStorage, sessionStorage, IndexedDB, WebSocket 같은 브라우저/네트워크 의존성은 mock, fake, fixture, test double을 우선 사용한다.
- 구현 세부사항보다 사용자가 관찰할 수 있는 결과, DOM 변화, 상태 전이, 이벤트, API 요청 payload를 검증한다.
- 스타일 자체보다 동작과 접근성에 영향을 주는 상태를 우선 검증한다.
- 테스트 때문에 생성된 파일, 스냅샷, 다운로드 결과물은 필요한 경우에만 정리하고, 사용자가 만든 파일을 임의로 삭제하지 않는다.

## 프로젝트 구조 확인

먼저 client 구조와 패키지 파일을 찾는다.

```bash
find . -maxdepth 3 -name package.json -o -name vite.config.* -o -name next.config.* -o -name playwright.config.* -o -name vitest.config.* -o -name jest.config.*
```

zsh에서 glob 에러가 나면 `rg --files`를 사용한다.

```bash
rg --files -g 'package.json' -g 'vite.config.*' -g 'next.config.*' -g 'playwright.config.*' -g 'vitest.config.*' -g 'jest.config.*'
```

테스트 도구는 보통 다음 파일이나 dependency로 확인한다.

- Vitest: `vitest`, `vite.config.*`, `vitest.config.*`
- Jest: `jest`, `jest.config.*`, `babel-jest`, `ts-jest`
- Testing Library: `@testing-library/dom`, `@testing-library/react`, `@testing-library/user-event`
- Playwright: `@playwright/test`, `playwright.config.*`
- Cypress: `cypress`, `cypress.config.*`
- MSW: `msw`

현재 저장소처럼 client가 정적 ES module 구조라면 `client/index.html`, `client/src/main.js`, `client/src/**`를 먼저 확인한다. 별도 번들러가 없으면 브라우저 기반 E2E 테스트와 순수 함수 단위 테스트를 우선 고려한다.

## 테스트 작성 우선순위

테스트는 비용 대비 효과가 큰 순서로 작성한다.

1. 순수 로직 테스트
   - 대화 선택지 정규화/검증
   - 게임 상태 전이
   - 전투 데미지, 턴, 승패 처리
   - 데이터 로딩과 fallback 처리
   - API 응답 파싱과 sanitizing

2. DOM/컴포넌트 테스트
   - 버튼 클릭, 입력, 키보드 이벤트
   - 로딩/에러/빈 상태 표시
   - 모달, 메뉴, HUD, 대화창 상태
   - localStorage 저장/복원

3. 브라우저 E2E 테스트
   - 게임 시작
   - 스타팅 선택
   - 필드 이동
   - 대화 진행
   - 전투 시작 후 공격/스킬/도망 플로우
   - 새로고침 후 상태 복구

단순 CSS, 정적 마크업, 라이브러리 기본 동작은 테스트 우선순위를 낮춘다.

## 테스트 실행

먼저 `package.json`의 scripts를 확인한다.

```bash
npm run
```

일반적인 실행 명령:

```bash
npm test
npm run test
npm run test:unit
npm run test:e2e
npm run test:ui
```

Vitest:

```bash
npx vitest run
npx vitest run path/to/file.test.js
npx vitest --watch
```

Jest:

```bash
npx jest
npx jest path/to/file.test.js
npx jest --watch
```

Playwright:

```bash
npx playwright test
npx playwright test tests/e2e/start-game.spec.js
npx playwright test --headed
npx playwright test --ui
```

프로젝트에 해당 도구가 설치되어 있지 않으면 임의로 `npx`로 다운로드하지 말고 사용자에게 도입 여부를 확인한다.

## 테스트 범위 좁히기

Vitest/Jest에서 테스트 이름으로 필터링한다.

```bash
npx vitest run -t "starter"
npx jest -t "starter"
```

Playwright에서 특정 브라우저나 파일만 실행한다.

```bash
npx playwright test tests/e2e/battle.spec.js --project=chromium
```

실패 분석 중에는 관련 테스트만 반복 실행하고, 수정이 끝난 뒤 가능한 범위의 전체 테스트를 실행한다.

## 테스트 작성 위치

기존 테스트가 있으면 그 구조를 따른다. 새로 만들 때는 다음 중 하나를 선택한다.

```text
client/src/battle/createBattleRuntime.test.js
client/src/game/createGameRuntime.test.js
client/tests/unit/battle.test.js
client/tests/e2e/start-game.spec.js
tests/e2e/client/start-game.spec.js
```

대상 파일과 가까운 곳에 두는 방식과 `tests/` 아래에 모으는 방식 중 기존 프로젝트 규칙을 우선한다.

## 정적 ES Module Client 테스트

현재 저장소처럼 `client/index.html`이 `client/src/main.js`를 직접 로드하는 구조에서는 다음 기준을 따른다.

- DOM을 직접 잡는 런타임은 테스트 전에 필요한 HTML fixture를 만든다.
- `document.getElementById`, `canvas.getContext`, `requestAnimationFrame`, `localStorage`, `fetch`는 테스트 환경에서 명시적으로 준비한다.
- 가능한 경우 DOM 접근이 많은 코드와 순수 로직을 분리해서 순수 로직을 먼저 테스트한다.
- 브라우저에서만 의미가 있는 캔버스 렌더링은 픽셀 단위보다 상태 변화와 호출 여부를 검증한다.
- 실제 `client/data/*.json`을 사용하는 테스트와 fixture 기반 테스트를 구분한다.

예시:

```js
import { describe, expect, it } from "vitest";

function normalizeChoiceText(choice) {
  return String(choice || "")
    .replace(/["'`]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

describe("normalizeChoiceText", () => {
  it("removes quotes and collapses spaces", () => {
    expect(normalizeChoiceText(' "같이   가자" ')).toBe("같이 가자");
  });
});
```

프로덕션 파일 안의 helper가 export되어 있지 않으면 테스트를 위해 무리하게 private 구현을 찌르지 않는다. public 동작으로 검증하거나, 실제로 재사용 가치가 있는 순수 helper만 별도 module로 분리한다.

## DOM 테스트

Testing Library를 사용하는 프로젝트에서는 role, label, text처럼 사용자 기준 query를 우선한다.

```js
import { fireEvent, screen } from "@testing-library/dom";
import { describe, expect, it } from "vitest";

describe("battle controls", () => {
  it("starts an attack when attack button is clicked", () => {
    document.body.innerHTML = `
      <button id="b-attack">공격</button>
      <div id="battle-log"></div>
    `;

    const button = screen.getByRole("button", { name: "공격" });
    fireEvent.click(button);

    expect(button).toBeDefined();
  });
});
```

DOM 테스트 규칙:

- `querySelector`보다 사용자 관점 query를 우선한다. 단, 기존 정적 HTML이 id 중심이면 id 사용을 허용한다.
- 비동기 UI는 `findBy*`, `waitFor`를 사용한다.
- 버튼 disabled, visible, text, aria 속성처럼 실제 사용자 영향이 있는 상태를 검증한다.
- layout pixel 값은 중요한 회귀 위험이 있을 때만 검증한다.

## React/Vue/Svelte 테스트

프레임워크가 있는 client라면 기존 stack에 맞춘다.

- React: `@testing-library/react`, `@testing-library/user-event`
- Vue: `@vue/test-utils`, Testing Library Vue
- Svelte: `@testing-library/svelte`
- Next.js: router, server action, fetch/cache 동작을 test double로 분리

React 예시:

```jsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { LoginForm } from "./LoginForm";

describe("LoginForm", () => {
  it("submits email and password", async () => {
    const onSubmit = vi.fn();
    render(<LoginForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText("Email"), "tester@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "password123");
    await userEvent.click(screen.getByRole("button", { name: "Login" }));

    expect(onSubmit).toHaveBeenCalledWith({
      email: "tester@example.com",
      password: "password123",
    });
  });
});
```

## API Mocking

외부 API와 서버 응답은 실제 네트워크보다 mock을 우선한다.

- 단위 테스트: `vi.fn()`, `jest.fn()`, fake `fetch`
- 브라우저/E2E: Playwright route mocking
- 앱 전체 mocking: MSW

fetch mock 예시:

```js
import { afterEach, describe, expect, it, vi } from "vitest";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("loadGameData", () => {
  it("loads maps and dialogues", async () => {
    vi.stubGlobal("fetch", vi.fn(async url => ({
      ok: true,
      json: async () => url.includes("maps") ? { start: {} } : { intro: [] },
    })));

    // const data = await loadGameData();
    // expect(data.maps.start).toBeDefined();
  });
});
```

LLM/API 키가 필요한 기능은 테스트에서 실제 secret을 사용하지 않는다. API 응답 shape을 fixture로 만들고 fallback, sanitizing, 에러 처리를 검증한다.

## WebSocket/실시간 테스트

멀티플레이나 실시간 전투 테스트는 계층을 나눈다.

- 메시지 생성/파싱: 순수 함수 단위 테스트
- 연결 상태 전이: fake socket으로 테스트
- 실제 서버 연동: 별도 integration/E2E 테스트

실제 서버가 필요한 테스트는 로컬 실행 조건, 포트, seed data, teardown 방법을 명확히 문서화한다. 운영 서버에는 연결하지 않는다.

## Playwright E2E

정적 client나 실제 브라우저 동작은 Playwright로 검증한다.

```js
import { expect, test } from "@playwright/test";

test("starts the game", async ({ page }) => {
  await page.goto("/");

  await expect(page.locator("#game")).toBeVisible();
  await page.keyboard.press("Enter");

  await expect(page.locator("#hud")).toBeVisible();
});
```

E2E 규칙:

- 테스트 시작 전에 필요한 서버 실행 방법을 확인한다.
- 테스트 데이터는 고정 fixture나 seed를 사용한다.
- animation, timer, network 지연 때문에 flake가 생기지 않도록 명시적인 locator와 condition을 기다린다.
- 스크린샷 테스트는 꼭 필요한 화면 회귀에만 사용한다.
- 실패 시 trace, screenshot, video 설정이 있으면 결과를 확인한다.

## Canvas 테스트

canvas 기반 게임 화면은 다음 순서로 검증한다.

- canvas element가 생성되고 크기가 맞는지 확인한다.
- draw 함수가 예외 없이 실행되는지 확인한다.
- 핵심 상태 변화가 렌더링 함수에 전달되는지 확인한다.
- 시각 회귀가 중요하면 Playwright screenshot이나 pixel check를 제한적으로 사용한다.

단위 테스트에서 `getContext("2d")`가 필요하면 fake context를 제공한다.

```js
HTMLCanvasElement.prototype.getContext = () => ({
  fillRect() {},
  beginPath() {},
  arc() {},
  fill() {},
  stroke() {},
  fillText() {},
  createLinearGradient: () => ({ addColorStop() {} }),
});
```

## localStorage와 저장 상태

저장/복원 테스트는 다음을 확인한다.

- 저장 key 이름
- 저장 payload shape
- 깨진 JSON이나 누락 필드 fallback
- 버전 변경 시 migration 또는 초기화 동작
- 새로고침 후 복원되는 사용자 관찰 상태

테스트마다 storage를 초기화한다.

```js
beforeEach(() => {
  localStorage.clear();
});
```

## 접근성 기본 검증

중요 UI는 접근성 회귀도 같이 본다.

- 버튼은 실제 `<button>` 또는 keyboard로 조작 가능해야 한다.
- 입력은 label 또는 aria-label이 있어야 한다.
- 모달/메뉴는 focus 이동과 Escape 처리를 확인한다.
- canvas 중심 UI는 필요한 경우 대체 텍스트나 상태 표시 DOM을 제공한다.

접근성 도구가 이미 있으면 `axe` 계열 테스트를 따른다. 새 도구 도입은 사용자에게 확인한다.

## 검증 명령

가능한 경우 테스트 전후로 lint/build/typecheck를 실행한다. 단, 프로젝트에 없는 스크립트를 임의로 만들거나 도입하지 않는다.

```bash
npm run lint
npm run typecheck
npm run build
npm test
```

정적 client라면 간단한 smoke 검증도 유용하다.

```bash
python3 -m http.server 8000 -d client
```

서버를 띄운 뒤 브라우저나 Playwright로 `http://127.0.0.1:8000`을 확인한다.

## 실패 분석 순서

1. 실패한 테스트 이름, 에러 메시지, stack trace를 확인한다.
2. 관련 테스트 파일과 대상 client 코드를 함께 읽는다.
3. 테스트가 잘못된 것인지, 구현이 잘못된 것인지 구분한다.
4. DOM fixture, mock, async wait, timer, storage 초기화 누락을 확인한다.
5. 관련 테스트만 재실행한다.
6. 수정 후 가능한 범위의 전체 client 테스트를 실행한다.

환경 문제, 누락된 secret, 실제 서버 의존성, 브라우저 설치 문제처럼 로컬에서 확정할 수 없는 원인은 사용자에게 확인한다.
