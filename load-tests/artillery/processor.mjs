const directions = ["Left", "Right", "Up", "Down"];

export function connectIsolatedRoom(params, context, next) {
  createRoom(context)
    .then((room) => {
      context.vars.roomId = room.roomId;
      context.vars.playerName = playerName(context);
      context.vars.sequence = 0;
      context.vars.paceMs = movementPaceMs(context);
      params.target = websocketUrl(room.roomId, context.vars.playerName, params.target);
      next();
    })
    .catch(next);
}

export function connectHotRoom(params, context, next) {
  getHotRoomId()
    .then((roomId) => {
      context.vars.roomId = roomId;
      context.vars.playerName = playerName(context);
      context.vars.sequence = 0;
      context.vars.paceMs = movementPaceMs(context);
      params.target = websocketUrl(roomId, context.vars.playerName, params.target);
      next();
    })
    .catch(next);
}

export function nextMove(context, events, next) {
  const sequence = Number(context.vars.sequence ?? 0) + 1;
  context.vars.sequence = sequence;

  const direction = directions[(sequence + hashCode(String(context.vars.playerName))) % directions.length];
  context.vars.moveMessage = JSON.stringify({
    type: "move",
    direction,
    sequence
  });

  next();
}

export async function paceMove(context) {
  await sleep(Number(context.vars.paceMs ?? 100));
}

async function createRoom(context) {
  const response = await fetch(`${httpBaseUrl()}/api/rooms`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      roomName: `artillery-${context.vars.$uuid ?? crypto.randomUUID()}`,
      mapId: "hometown"
    })
  });

  if (!response.ok) {
    throw new Error(`room creation failed: ${response.status} ${await response.text()}`);
  }

  return response.json();
}

async function getHotRoomId() {
  if (process.env.POKEMON2_ROOM_ID) {
    return process.env.POKEMON2_ROOM_ID;
  }

  const response = await fetch(`${httpBaseUrl()}/api/rooms`);
  if (!response.ok) {
    throw new Error(`room lookup failed: ${response.status} ${await response.text()}`);
  }

  const rooms = await response.json();
  const firstRoom = rooms[0];
  if (!firstRoom?.roomId) {
    throw new Error("room lookup returned no rooms");
  }

  return firstRoom.roomId;
}

function websocketUrl(roomId, name, fallbackTarget) {
  const base = process.env.POKEMON2_WS_TARGET ?? fallbackTarget ?? "ws://localhost:5199";
  return `${base}/ws/game?roomId=${encodeURIComponent(roomId)}&playerName=${encodeURIComponent(name)}`;
}

function httpBaseUrl() {
  return process.env.POKEMON2_HTTP_TARGET ?? "http://localhost:5199";
}

function playerName(context) {
  const id = context.vars.$uuid ?? crypto.randomUUID();
  return `art-${String(id).slice(0, 8)}`;
}

function movementPaceMs(context) {
  const pace = String(context.vars.pace ?? "normal");
  if (pace === "fast") return 40;
  if (pace === "slow") return 250;
  return 110;
}

function hashCode(value) {
  let hash = 0;
  for (let i = 0; i < value.length; i++) {
    hash = (hash * 31 + value.charCodeAt(i)) | 0;
  }

  return Math.abs(hash);
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
