const MOVE_REJECT_MESSAGES = {
  tile_occupied: "다른 플레이어가 있는 칸이라 이동할 수 없습니다.",
  speed_hack_detected: "이동 요청이 너무 빨라 서버에서 거부했습니다.",
  stale_sequence: "오래된 이동 요청이라 서버에서 무시했습니다.",
  wall_collision: "벽이나 막힌 지형이라 이동할 수 없습니다.",
};

export function describeMoveRejectReason(reason) {
  return MOVE_REJECT_MESSAGES[reason] || "서버에서 이동을 거부했습니다.";
}

export function describeSocketClose({ code, reason }) {
  if (reason === "Room is full") {
    return "방이 가득 차서 입장할 수 없습니다. 다른 방을 선택하거나 잠시 후 다시 시도하세요.";
  }

  if (reason === "Reconnected from a newer session.") {
    return "더 새로운 연결이 생겨 현재 세션을 종료했습니다.";
  }

  if (code === 1006) {
    return "서버와의 연결이 갑자기 끊겼습니다.";
  }

  if (code === 1008) {
    return reason
      ? `서버가 연결을 거부했습니다: ${reason}`
      : "서버가 현재 연결을 허용하지 않았습니다.";
  }

  if (code === 1000) {
    return reason || "멀티플레이 연결을 종료했습니다.";
  }

  return reason
    ? `멀티플레이 연결이 종료되었습니다: ${reason}`
    : `멀티플레이 연결이 종료되었습니다. (코드 ${code})`;
}

export function isReconnectableClose({ code, reason }) {
  if (reason === "Room is full" || reason === "Reconnected from a newer session.") {
    return false;
  }

  return code !== 1000;
}

export function describeJoinError(status) {
  switch (status) {
    case 401:
      return "사용자 식별이 만료되어 다시 인증한 뒤 재입장해야 합니다.";
    case 404:
      return "방을 찾을 수 없어 다시 입장할 수 없습니다.";
    case 409:
      return "서버가 현재 재입장을 허용하지 않았습니다.";
    case 502:
    case 503:
      return "서버가 일시적으로 응답하지 않아 재입장에 실패했습니다.";
    default:
      return status
        ? `재입장 요청이 실패했습니다. (HTTP ${status})`
        : "재입장 요청에 실패했습니다.";
  }
}

export function buildReconnectMessage({ attempt, maxAttempts, delayMs }) {
  const seconds = Math.max(1, Math.ceil(delayMs / 1000));
  return `연결이 끊겼습니다. ${seconds}초 후 자동 재접속합니다. (${attempt}/${maxAttempts})`;
}

export function parseHttpStatus(error) {
  const match = String(error?.message || "").match(/HTTP\s+(\d{3})/i);
  return match ? Number(match[1]) : null;
}
