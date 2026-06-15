import assert from "node:assert/strict";
import { afterEach, describe, it } from "node:test";

import { bootstrapClient } from "./bootstrap.js";

const originalGlobals = {
  document: globalThis.document,
  window: globalThis.window,
  localStorage: globalThis.localStorage,
  fetch: globalThis.fetch,
  requestAnimationFrame: globalThis.requestAnimationFrame,
  cancelAnimationFrame: globalThis.cancelAnimationFrame,
  WebSocket: globalThis.WebSocket,
};

afterEach(() => {
  for (const [key, value] of Object.entries(originalGlobals)) {
    if (value === undefined) {
      delete globalThis[key];
      continue;
    }

    globalThis[key] = value;
  }
});

describe("bootstrapClient smoke test", () => {
  it("boots the client runtimes without throwing on a static page load", async () => {
    const { document, elements } = installFakeBrowserEnv();
    globalThis.document = document;
    globalThis.window = globalThis;
    globalThis.localStorage = createLocalStorage();
    globalThis.requestAnimationFrame = callback => {
      return setTimeout(() => callback(Date.now()), 0);
    };
    globalThis.cancelAnimationFrame = handle => clearTimeout(handle);
    globalThis.WebSocket = class FakeWebSocket {};

    const fixtures = {
      "data/maps.json": {
        hometown: {
          name: "시작 마을",
          width: 20,
          height: 15,
          tileSize: 32,
          bgColor: "#90EE90",
          wallStyle: "building",
          tileColors: { 0: "#90EE90", 1: "#228B22", 2: "#A0522D", 4: "#4169E1" },
          tiles: Array.from({ length: 15 }, () => Array.from({ length: 20 }, () => 0)),
          npcs: [],
          doors: [],
          autoEvents: [],
          playerStart: { tx: 9, ty: 9 },
        },
      },
      "data/dialogues.json": {
        mom: ["다녀오렴."],
        professor_after: ["조심히 가거라."],
        autoEvents: {},
        rivalStarterReaction: {},
      },
    };

    globalThis.fetch = async path => ({
      ok: true,
      async json() {
        return fixtures[path];
      },
    });

    const { game, battle } = await bootstrapClient();

    assert.ok(game);
    assert.ok(battle);
    assert.equal(elements.get("game-canvas").width, 640);
    assert.equal(elements.get("battle-canvas").width, 640);
    assert.equal(
      elements.get("save-status-text").textContent,
      "서버 저장 상태를 아직 확인하지 않았습니다."
    );
    assert.equal(elements.get("save-status-badge").textContent, "저장 대기");
  });
});

function installFakeBrowserEnv() {
  const elements = new Map();

  const document = {
    getElementById(id) {
      if (!elements.has(id)) {
        elements.set(id, createElement(id));
      }
      return elements.get(id);
    },
    addEventListener() {},
  };

  return { document, elements };
}

function createElement(id) {
  const canvasContext = createCanvasContext();
  return {
    id,
    value: "",
    textContent: "",
    innerHTML: "",
    disabled: false,
    width: 0,
    height: 0,
    style: { display: "", opacity: "" },
    dataset: {},
    onclick: null,
    addEventListener() {},
    appendChild() {},
    removeChild() {},
    getContext() {
      return canvasContext;
    },
    classList: {
      toggle() {},
      add() {},
      remove() {},
    },
  };
}

function createCanvasContext() {
  const gradient = { addColorStop() {} };
  return {
    fillRect() {},
    strokeRect() {},
    clearRect() {},
    beginPath() {},
    arc() {},
    ellipse() {},
    fill() {},
    stroke() {},
    moveTo() {},
    lineTo() {},
    closePath() {},
    roundRect() {},
    save() {},
    restore() {},
    translate() {},
    scale() {},
    fillText() {},
    strokeText() {},
    measureText(text) {
      return { width: String(text).length * 8 };
    },
    createLinearGradient() {
      return gradient;
    },
    set fillStyle(_) {},
    set strokeStyle(_) {},
    set lineWidth(_) {},
    set font(_) {},
    set textAlign(_) {},
    set globalAlpha(_) {},
  };
}

function createLocalStorage() {
  const storage = new Map();
  return {
    getItem(key) {
      return storage.has(key) ? storage.get(key) : null;
    },
    setItem(key, value) {
      storage.set(key, String(value));
    },
    removeItem(key) {
      storage.delete(key);
    },
  };
}
