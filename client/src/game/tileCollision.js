const BLOCKED_TILE_TYPES = new Set([1, 4]);

export function isBlockedTile(tileType) {
  return BLOCKED_TILE_TYPES.has(Number(tileType));
}
