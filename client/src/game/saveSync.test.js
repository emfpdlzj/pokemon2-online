import assert from "node:assert/strict";
import { describe, it } from "node:test";
import {
  buildSaveEnvelope,
  describeSaveResolution,
  normalizeStoredSaveEnvelope,
  resolveSaveConflict,
} from "./saveSync.js";

describe("save sync helpers", () => {
  it("normalizes persisted envelopes and legacy save payloads", () => {
    const envelope = normalizeStoredSaveEnvelope({
      mode: "multi",
      slotNumber: 2,
      gameState: { currentMap: "route1" },
      savedAt: "2026-06-15T04:00:00.000Z",
      pendingSync: true,
    });
    assert.equal(envelope.mode, "multi");
    assert.equal(envelope.slotNumber, 2);
    assert.equal(envelope.pendingSync, true);

    const legacy = normalizeStoredSaveEnvelope({ currentMap: "hometown", px: 9 });
    assert.equal(legacy.mode, "single");
    assert.equal(legacy.gameState.currentMap, "hometown");
  });

  it("builds save envelopes and resolves local-only state", () => {
    const envelope = buildSaveEnvelope({
      mode: "single",
      slotNumber: 5,
      gameState: { currentMap: "hometown" },
      savedAt: "2026-06-15T04:00:00.000Z",
      pendingSync: true,
    });
    assert.equal(envelope.slotNumber, 3);

    const resolution = resolveSaveConflict({ localEnvelope: envelope, serverUpdatedAt: null });
    assert.deepEqual(resolution, { preferredSource: "local", state: "local_only" });
    assert.equal(describeSaveResolution(resolution.state), "서버 저장 없음 · 브라우저 임시본만 있음");
  });

  it("prefers the newer copy between local and server saves", () => {
    const localEnvelope = buildSaveEnvelope({
      mode: "single",
      slotNumber: 1,
      gameState: { currentMap: "route1" },
      savedAt: "2026-06-15T04:10:00.000Z",
      pendingSync: true,
    });
    assert.deepEqual(
      resolveSaveConflict({ localEnvelope, serverUpdatedAt: "2026-06-15T04:05:00.000Z" }),
      { preferredSource: "local", state: "local_newer" }
    );
    assert.deepEqual(
      resolveSaveConflict({ localEnvelope, serverUpdatedAt: "2026-06-15T04:20:00.000Z" }),
      { preferredSource: "server", state: "server_newer" }
    );
  });
});
