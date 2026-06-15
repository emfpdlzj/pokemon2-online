export function upsertEntityRenderState(store, entityId, position, {
  now,
  tileSize,
  duration,
} = {}) {
  if (!store || !entityId || !position) return null;

  const targetX = position.x * tileSize;
  const targetY = position.y * tileSize;
  const existing = store.get(entityId);
  if (!existing) {
    const created = {
      renderX: targetX,
      renderY: targetY,
      startX: targetX,
      startY: targetY,
      targetX,
      targetY,
      startTs: now,
      duration,
    };
    store.set(entityId, created);
    return created;
  }

  const current = readEntityRenderPosition(store, entityId, now) || { x: targetX, y: targetY };
  if (existing.targetX === targetX && existing.targetY === targetY) {
    existing.renderX = current.x;
    existing.renderY = current.y;
    return existing;
  }

  existing.renderX = current.x;
  existing.renderY = current.y;
  existing.startX = current.x;
  existing.startY = current.y;
  existing.targetX = targetX;
  existing.targetY = targetY;
  existing.startTs = now;
  existing.duration = duration;
  return existing;
}

export function readEntityRenderPosition(store, entityId, now) {
  const state = store?.get(entityId);
  if (!state) return null;
  if (!state.duration || state.duration <= 0) {
    state.renderX = state.targetX;
    state.renderY = state.targetY;
    return { x: state.renderX, y: state.renderY };
  }

  const elapsed = Math.max(0, now - state.startTs);
  const progress = Math.min(1, elapsed / state.duration);
  state.renderX = state.startX + (state.targetX - state.startX) * progress;
  state.renderY = state.startY + (state.targetY - state.startY) * progress;
  if (progress >= 1) {
    state.startX = state.targetX;
    state.startY = state.targetY;
  }

  return { x: state.renderX, y: state.renderY };
}

export function pruneEntityRenderStates(store, activeIds) {
  if (!store) return;
  store.forEach((_, entityId) => {
    if (!activeIds.has(entityId)) {
      store.delete(entityId);
    }
  });
}
