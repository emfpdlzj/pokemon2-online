import assert from "node:assert/strict";
import { afterEach, describe, it } from "node:test";

import { loadGameData } from "./gameData.js";

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
});

describe("loadGameData", () => {
  it("loads maps and dialogues from the client data directory", async () => {
    const calls = [];
    const fixtures = {
      "data/maps.json": { hometown: { name: "시작마을" } },
      "data/dialogues.json": { mom: ["다녀오렴."] },
    };

    globalThis.fetch = async path => {
      calls.push(path);
      return {
        ok: true,
        async json() {
          return fixtures[path];
        },
      };
    };

    assert.deepEqual(await loadGameData(), {
      maps: fixtures["data/maps.json"],
      dialogues: fixtures["data/dialogues.json"],
    });
    assert.deepEqual(calls, ["data/maps.json", "data/dialogues.json"]);
  });

  it("throws a path-specific error when a data file cannot be loaded", async () => {
    globalThis.fetch = async path => ({
      ok: path !== "data/dialogues.json",
      status: 404,
      async json() {
        return {};
      },
    });

    await assert.rejects(
      loadGameData(),
      /Failed to load data\/dialogues\.json: HTTP 404/
    );
  });
});
