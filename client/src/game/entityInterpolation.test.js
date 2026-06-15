import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  pruneEntityRenderStates,
  readEntityRenderPosition,
  upsertEntityRenderState,
} from "./entityInterpolation.js";

describe("entity interpolation helpers", () => {
  it("stores the first position at the exact tile coordinates", () => {
    const store = new Map();

    upsertEntityRenderState(store, "p1", { x: 3, y: 4 }, {
      now: 100,
      tileSize: 32,
      duration: 180,
    });

    assert.deepEqual(readEntityRenderPosition(store, "p1", 100), { x: 96, y: 128 });
  });

  it("interpolates smoothly toward the next tile", () => {
    const store = new Map();
    upsertEntityRenderState(store, "p1", { x: 1, y: 1 }, { now: 0, tileSize: 32, duration: 160 });
    upsertEntityRenderState(store, "p1", { x: 2, y: 1 }, { now: 0, tileSize: 32, duration: 160 });

    assert.deepEqual(readEntityRenderPosition(store, "p1", 80), { x: 48, y: 32 });
    assert.deepEqual(readEntityRenderPosition(store, "p1", 160), { x: 64, y: 32 });
  });

  it("starts a new movement from the current rendered position when updates arrive mid-motion", () => {
    const store = new Map();
    upsertEntityRenderState(store, "p1", { x: 1, y: 1 }, { now: 0, tileSize: 32, duration: 200 });
    upsertEntityRenderState(store, "p1", { x: 2, y: 1 }, { now: 0, tileSize: 32, duration: 200 });

    assert.deepEqual(readEntityRenderPosition(store, "p1", 100), { x: 48, y: 32 });

    upsertEntityRenderState(store, "p1", { x: 3, y: 1 }, { now: 100, tileSize: 32, duration: 200 });

    assert.deepEqual(readEntityRenderPosition(store, "p1", 200), { x: 72, y: 32 });
    assert.deepEqual(readEntityRenderPosition(store, "p1", 300), { x: 96, y: 32 });
  });

  it("prunes render states for entities missing from the latest snapshot", () => {
    const store = new Map();
    upsertEntityRenderState(store, "p1", { x: 1, y: 1 }, { now: 0, tileSize: 32, duration: 200 });
    upsertEntityRenderState(store, "p2", { x: 2, y: 1 }, { now: 0, tileSize: 32, duration: 200 });

    pruneEntityRenderStates(store, new Set(["p2"]));

    assert.equal(store.has("p1"), false);
    assert.equal(store.has("p2"), true);
  });
});
