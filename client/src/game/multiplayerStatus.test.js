import assert from "node:assert/strict";
import { describe, it } from "node:test";
import {
  buildReconnectMessage,
  describeJoinError,
  describeMoveRejectReason,
  describeSocketClose,
  isReconnectableClose,
  parseHttpStatus,
} from "./multiplayerStatus.js";

describe("multiplayer status helpers", () => {
  it("maps move rejection reasons to user-facing text", () => {
    assert.equal(
      describeMoveRejectReason("speed_hack_detected"),
      "이동 요청이 너무 빨라 서버에서 거부했습니다."
    );
    assert.equal(
      describeMoveRejectReason("unknown_reason"),
      "서버에서 이동을 거부했습니다."
    );
  });

  it("describes websocket close cases and reconnectability", () => {
    assert.equal(
      describeSocketClose({ code: 1008, reason: "Room is full" }),
      "방이 가득 차서 입장할 수 없습니다. 다른 방을 선택하거나 잠시 후 다시 시도하세요."
    );
    assert.equal(
      describeSocketClose({ code: 1006, reason: "" }),
      "서버와의 연결이 갑자기 끊겼습니다."
    );
    assert.equal(isReconnectableClose({ code: 1006, reason: "" }), true);
    assert.equal(isReconnectableClose({ code: 1000, reason: "Reconnected from a newer session." }), false);
  });

  it("formats reconnect and join failure messages", () => {
    assert.equal(
      buildReconnectMessage({ attempt: 2, maxAttempts: 3, delayMs: 2000 }),
      "연결이 끊겼습니다. 2초 후 자동 재접속합니다. (2/3)"
    );
    assert.equal(
      describeJoinError(404),
      "방을 찾을 수 없어 다시 입장할 수 없습니다."
    );
  });

  it("extracts HTTP status codes from thrown fetch errors", () => {
    assert.equal(parseHttpStatus(new Error("HTTP 503")), 503);
    assert.equal(parseHttpStatus(new Error("network failed")), null);
  });
});
