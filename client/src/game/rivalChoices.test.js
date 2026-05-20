import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildFallbackChoices,
  classifyRivalMessage,
  normalizeChoiceText,
  sanitizeRivalChoices,
} from "./rivalChoices.js";

describe("rival choice helpers", () => {
  it("classifies rival messages by observable intent", () => {
    assert.equal(classifyRivalMessage("네 파트너 파이리, 꽤 괜찮아 보이네."), "starter");
    assert.equal(classifyRivalMessage("앞쪽 길은 위험하니까 조심해."), "warning");
    assert.equal(classifyRivalMessage("흥, 내가 더 강해질 거야."), "taunt");
    assert.equal(classifyRivalMessage("어디로 갈 건데?"), "question");
    assert.equal(classifyRivalMessage("안녕, 기다리고 있었어."), "greeting");
    assert.equal(classifyRivalMessage(""), "generic");
  });

  it("normalizes model choice text before validation", () => {
    assert.equal(normalizeChoiceText(' "같이   가볼래?" '), "같이 가볼래?");
  });

  it("removes unsafe, duplicate, and off-topic choices then backfills to three", () => {
    const choices = sanitizeRivalChoices(
      [
        '"조심할게."',
        "조심할게.",
        "체육관부터 가자",
        "덤벼",
        "뭐가 있는 거야?",
      ],
      "앞쪽 길은 위험하니까 조심해."
    );

    assert.deepEqual(choices, ["조심할게.", "뭐가 있는 거야?", "넌 안 무서워?"]);
  });

  it("returns relevant fallback choices when model choices are unusable", () => {
    assert.deepEqual(buildFallbackChoices("네 몬스터는 박사님한테 받은 거야?"), [
      "내 파트너야.",
      "잘 어울리지?",
      "금방 강해질걸.",
    ]);
  });
});

