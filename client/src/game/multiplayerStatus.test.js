import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildReconnectMessage,
  describeJoinError,
  describeSocketClose,
  isReconnectableClose,
} from "./multiplayerStatus.js";

describe("multiplayer status helpers", () => {
  it("describes room-full socket closures clearly", () => {
    assert.equal(
      describeSocketClose({ code: 1008, reason: "Room is full" }),
      "방이 가득 차서 입장할 수 없습니다. 다른 방을 선택하거나 잠시 후 다시 시도하세요."
    );
    assert.equal(
      isReconnectableClose({ code: 1008, reason: "Room is full" }),
      false
    );
  });

  it("separates initial join failures from reconnect failures", () => {
    assert.equal(
      describeJoinError(409),
      "방이 이미 가득 찼거나 서버가 현재 입장을 허용하지 않았습니다."
    );
    assert.equal(
      describeJoinError(409, { reconnecting: true }),
      "서버가 현재 재입장을 허용하지 않았습니다. 방 목록으로 돌아가 다시 입장하세요."
    );
  });

  it("builds reconnect status messages with attempt counts", () => {
    assert.equal(
      buildReconnectMessage({ attempt: 2, maxAttempts: 3, delayMs: 1800 }),
      "연결이 끊겼습니다. 2초 후 자동 재접속합니다. (2/3)"
    );
  });
});
