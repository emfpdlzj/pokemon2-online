export function normalizeStoredSaveEnvelope(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return null;
  }

  if ("gameState" in value && value.gameState && typeof value.gameState === "object") {
    return {
      schemaVersion: 1,
      mode: value.mode === "multi" ? "multi" : "single",
      slotNumber: Number(value.slotNumber) || 1,
      gameState: value.gameState,
      savedAt: typeof value.savedAt === "string" ? value.savedAt : null,
      serverUpdatedAt: typeof value.serverUpdatedAt === "string" ? value.serverUpdatedAt : null,
      pendingSync: Boolean(value.pendingSync),
      syncError: typeof value.syncError === "string" ? value.syncError : null,
    };
  }

  return {
    schemaVersion: 1,
    mode: value.mode === "multi" ? "multi" : "single",
    slotNumber: 1,
    gameState: value,
    savedAt: null,
    serverUpdatedAt: null,
    pendingSync: false,
    syncError: null,
  };
}

export function buildSaveEnvelope({
  mode,
  slotNumber,
  gameState,
  savedAt,
  serverUpdatedAt = null,
  pendingSync = false,
  syncError = null,
}) {
  return {
    schemaVersion: 1,
    mode: mode === "multi" ? "multi" : "single",
    slotNumber: Math.min(3, Math.max(1, Number(slotNumber) || 1)),
    gameState,
    savedAt,
    serverUpdatedAt,
    pendingSync,
    syncError,
  };
}

export function resolveSaveConflict({ localEnvelope, serverUpdatedAt }) {
  const localTimestamp = toTimestamp(localEnvelope?.savedAt);
  const serverTimestamp = toTimestamp(serverUpdatedAt);

  if (!localEnvelope?.gameState && !serverUpdatedAt) {
    return { preferredSource: "empty", state: "empty" };
  }

  if (!localEnvelope?.gameState) {
    return { preferredSource: "server", state: "server_only" };
  }

  if (!serverUpdatedAt) {
    return { preferredSource: "local", state: "local_only" };
  }

  if (localTimestamp == null || serverTimestamp == null) {
    return { preferredSource: "server", state: "server_only" };
  }

  if (localTimestamp > serverTimestamp) {
    return { preferredSource: "local", state: "local_newer" };
  }

  if (localTimestamp < serverTimestamp) {
    return { preferredSource: "server", state: "server_newer" };
  }

  return { preferredSource: "server", state: "synced" };
}

export function describeSaveResolution(state) {
  switch (state) {
    case "local_only":
      return "서버 저장 없음 · 브라우저 임시본만 있음";
    case "local_newer":
      return "로컬 임시본이 더 최신 · 서버 재동기화 필요";
    case "server_newer":
      return "서버 저장이 더 최신 · 서버 본 우선";
    case "synced":
      return "서버와 로컬 저장 시각이 맞춰져 있음";
    default:
      return "";
  }
}

function toTimestamp(value) {
  if (!value) return null;
  const timestamp = Date.parse(value);
  return Number.isFinite(timestamp) ? timestamp : null;
}
