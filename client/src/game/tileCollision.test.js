import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { isBlockedTile } from "./tileCollision.js";

describe("tile collision rules", () => {
  it("blocks walls and water tiles", () => {
    assert.equal(isBlockedTile(1), true);
    assert.equal(isBlockedTile(4), true);
  });

  it("keeps walkable ground and doors enterable", () => {
    assert.equal(isBlockedTile(0), false);
    assert.equal(isBlockedTile(2), false);
    assert.equal(isBlockedTile(5), false);
  });
});
