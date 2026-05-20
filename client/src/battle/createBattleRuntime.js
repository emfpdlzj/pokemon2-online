export function createBattleRuntime(game) {
const {
  state,
  maps: MAPS,
  normalizeStarter,
  getStarterTemplate,
  getStarterStats,
  createStarterFromTemplate,
  awardStarterExp,
  saveGame,
  updateHUD,
  sendBattleEvent,
} = game;

// ============================================================
//  턴제 전투 시스템
// ============================================================

// ── 야생/트레이너 몬스터 데이터 ──
const WILD_MONSTERS = [
  { name: "풀벌레",  hp: 20, maxHp: 20, attack: 4,  color: "#7CFC00", emoji: "🐛", expReward: 8 },
  { name: "꼬마새",  hp: 18, maxHp: 18, attack: 5,  color: "#87CEEB", emoji: "🐦", expReward: 9 },
  { name: "모래쥐",  hp: 25, maxHp: 25, attack: 6,  color: "#D2B48C", emoji: "🐭", expReward: 10 },
];

const TRAINER_MONSTERS = {
  trainer1: { name: "불꽃여우", hp: 28, maxHp: 28, attack: 7, color: "#FF6347", emoji: "🦊", expReward: 16 },
};

// ── 전투 상태 ──
const battleState = {
  active: false,
  player: null,   // { name, hp, maxHp, attack, emoji, color }
  enemy:  null,
  log: [],
  playerTurn: true,
  skillUsesLeft: 0,
  animShake: null, // { target: "player"|"enemy", until: timestamp }
  onEnd: null,    // 전투 종료 콜백
  battleId: null,
  role: "host",   // host | joiner
  hostPlayerId: null,
  pendingAlly: null,
  ally: null,
  remoteOffer: null,
};

// ── DOM ──
const battleScreen  = document.getElementById("battle-screen");
const bCanvasEl     = document.getElementById("battle-canvas");
const bCtx          = bCanvasEl.getContext("2d");
const bLog          = document.getElementById("battle-log");
const bBtnAttack    = document.getElementById("b-attack");
const bBtnSkill     = document.getElementById("b-skill");
const bBtnTeam      = document.getElementById("b-team");
const bBtnRun       = document.getElementById("b-run");
const battleWaitingPanel = document.getElementById("battle-waiting-panel");
const battleWaitingTitle = document.getElementById("battle-waiting-title");
const battleWaitingText = document.getElementById("battle-waiting-text");
const battleJoinConfirm = document.getElementById("battle-join-confirm");
const MAX_SKILL_USES = 7;

const B_W = 640, B_H = 320;
bCanvasEl.width  = B_W;
bCanvasEl.height = B_H;

// ============================================================
//  전투 시작
// ============================================================
function startBattle(enemyData, onEnd, options = {}) {
  const starter = normalizeStarter(state.starter) || createStarterFromTemplate(getStarterTemplate("fire"));
  const starterStats = getStarterStats(starter);
  const startingHp = Math.max(1, Math.min(starter.currentHp ?? starterStats.maxHp, starterStats.maxHp));
  battleState.player = {
    name: starter.name,
    level: starterStats.level,
    hp: startingHp,
    maxHp: starterStats.maxHp,
    attack: starterStats.attack,
    emoji: starter.emoji || "⚡",
    color: starter.color,
  };
  battleState.enemy    = { ...enemyData };
  battleState.active   = true;
  battleState.playerTurn = true;
  battleState.skillUsesLeft = MAX_SKILL_USES;
  battleState.log      = [];
  battleState.animShake = null;
  battleState.onEnd    = onEnd || null;
  battleState.battleId = options.battleId || `battle-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  battleState.role = options.role || "host";
  battleState.hostPlayerId = options.hostPlayerId || null;
  battleState.pendingAlly = null;
  battleState.ally = options.ally || null;
  battleState.remoteOffer = null;

  state.phase = "battle";
  battleScreen.style.display = "flex";
  hideBattleWaitingPanel();
  updateSkillButtonLabel();
  setButtons(false);

  const introText = battleState.enemy.battleIntro || `야생의 ${battleState.enemy.name}이(가) 나타났다!`;
  if (battleState.role === "host") {
    broadcastBattleEvent("started", {
      enemy: sanitizeBattleMonster(battleState.enemy),
      hostName: state.playerName,
    });
    printLog(introText, () => {
      printLog(`${battleState.player.name}, 싸워라!`, startPlayerTurn);
    });
  } else {
    showBattleWaitingPanel("전투 참여 대기 중", "다음 유저 턴에 입장합니다.", false);
    printLog("팀원의 전투에 참여 대기 중입니다.");
  }

  requestAnimationFrame(battleLoop);
}

// ============================================================
//  전투 렌더링
// ============================================================
function battleLoop(ts) {
  if (!battleState.active) return;
  drawBattle(ts);
  requestAnimationFrame(battleLoop);
}

function drawBattle(ts) {
  // 맵 배경색 반영
  const mapBg = MAPS[state.currentMap]?.bgColor || "#1a3a1a";
  bCtx.fillStyle = "#162033";
  bCtx.fillRect(0, 0, B_W, B_H);

  // 배경 (맵 색상 기반)
  bCtx.fillStyle = mapBg;
  bCtx.globalAlpha = 0.45;
  bCtx.fillRect(0, 0, B_W, B_H);
  bCtx.globalAlpha = 1;

  // 상단 안개 그라데이션
  const sky = bCtx.createLinearGradient(0, 0, 0, B_H * 0.6);
  sky.addColorStop(0, "rgba(255,255,255,0.12)");
  sky.addColorStop(1, "rgba(255,255,255,0)");
  bCtx.fillStyle = sky;
  bCtx.fillRect(0, 0, B_W, B_H * 0.6);

  // 전투 바닥
  bCtx.fillStyle = "#325f1f";
  bCtx.fillRect(0, B_H * 0.58, B_W, B_H * 0.42);
  bCtx.fillStyle = "#4d8b2f";
  bCtx.fillRect(0, B_H * 0.58, B_W, 14);

  const shake = battleState.animShake;
  const shaking = shake && ts < shake.until;

  // 적 (오른쪽 위)
  let ex = B_W * 0.68, ey = B_H * 0.12;
  if (shaking && shake.target === "enemy") ex += Math.sin(ts * 0.08) * 6;
  drawMonster(bCtx, battleState.enemy, ex, ey, false);
  drawStatusPanel(bCtx, battleState.enemy, B_W - 250, 18, 220, false);

  // 플레이어 (왼쪽 아래)
  let px = B_W * 0.10, py = B_H * 0.44;
  if (shaking && shake.target === "player") px += Math.sin(ts * 0.08) * 6;
  drawMonster(bCtx, battleState.player, px, py, true);
  drawStatusPanel(bCtx, battleState.player, 24, B_H - 104, battleState.ally ? 210 : 240, true);

  if (battleState.ally) {
    let ax = B_W * 0.25, ay = B_H * 0.49;
    if (shaking && shake.target === "ally") ax += Math.sin(ts * 0.08) * 6;
    drawMonster(bCtx, battleState.ally, ax, ay, true);
    drawStatusPanel(bCtx, battleState.ally, B_W - 250, B_H - 104, 220, true);
  }
}

function drawMonster(ctx, mon, x, y, isPlayer) {
  const size = 80;
  // 그림자
  ctx.fillStyle = "rgba(0,0,0,0.3)";
  ctx.beginPath();
  ctx.ellipse(x + size/2, y + size + 6, size/2, 10, 0, 0, Math.PI*2);
  ctx.fill();
  // 몸통
  ctx.fillStyle = mon.color;
  ctx.beginPath();
  ctx.ellipse(x + size/2, y + size*0.55, size*0.38, size*0.32, 0, 0, Math.PI*2);
  ctx.fill();
  // 머리
  ctx.fillStyle = mon.color;
  ctx.beginPath();
  ctx.arc(x + size/2, y + size*0.3, size*0.28, 0, Math.PI*2);
  ctx.fill();
  // 눈
  ctx.fillStyle = "#fff";
  ctx.beginPath();
  ctx.arc(x + size*0.38, y + size*0.27, size*0.07, 0, Math.PI*2);
  ctx.arc(x + size*0.62, y + size*0.27, size*0.07, 0, Math.PI*2);
  ctx.fill();
  ctx.fillStyle = "#1a1a2e";
  ctx.beginPath();
  ctx.arc(x + size*0.39, y + size*0.27, size*0.04, 0, Math.PI*2);
  ctx.arc(x + size*0.63, y + size*0.27, size*0.04, 0, Math.PI*2);
  ctx.fill();
  // 밝은 하이라이트
  ctx.fillStyle = "rgba(255,255,255,0.25)";
  ctx.beginPath();
  ctx.ellipse(x + size*0.38, y + size*0.2, size*0.12, size*0.08, -0.5, 0, Math.PI*2);
  ctx.fill();
}

function drawStatusPanel(ctx, mon, x, y, w, isPlayer) {
  const ratio = Math.max(0, mon.hp / mon.maxHp);
  const barColor = ratio > 0.5 ? "#44dd44" : ratio > 0.25 ? "#dddd00" : "#dd2222";
  const h = 62;

  ctx.fillStyle = "rgba(10, 16, 28, 0.88)";
  ctx.fillRect(x, y, w, h);
  ctx.strokeStyle = isPlayer ? "#ffd54a" : "#c7d7ff";
  ctx.lineWidth = 2;
  ctx.strokeRect(x, y, w, h);

  ctx.fillStyle = "#ffffff";
  ctx.font = "bold 15px 'Courier New'";
  ctx.textAlign = "left";
  ctx.fillText(mon.name, x + 12, y + 18);

  if (mon.level) {
    ctx.fillStyle = isPlayer ? "#ffd54a" : "#d9e7ff";
    ctx.font = "bold 13px 'Courier New'";
    ctx.textAlign = "right";
    ctx.fillText(`Lv.${mon.level}`, x + w - 12, y + 18);
  }

  ctx.fillStyle = "#cfe3ff";
  ctx.font = "bold 11px 'Courier New'";
  ctx.textAlign = "left";
  ctx.fillText("HP", x + 12, y + 36);

  ctx.fillStyle = "#2d3550";
  ctx.fillRect(x + 40, y + 28, w - 52, 12);
  ctx.fillStyle = barColor;
  ctx.fillRect(x + 40, y + 28, (w - 52) * ratio, 12);
  ctx.strokeStyle = "#7f8ca8";
  ctx.lineWidth = 1;
  ctx.strokeRect(x + 40, y + 28, w - 52, 12);

  ctx.fillStyle = "#fff";
  ctx.font = "bold 11px 'Courier New'";
  ctx.textAlign = "right";
  ctx.fillText(`${mon.hp}/${mon.maxHp}`, x + w - 12, y + 54);
}

// ============================================================
//  전투 로직
// ============================================================
function playerAttack() {
  if (!canActInBattle()) return;
  setButtons(false);
  const dmg = calcDamage(battleState.player.attack);
  battleState.enemy.hp = Math.max(0, battleState.enemy.hp - dmg);
  shake("enemy");
  printLog(`${battleState.player.name}의 공격! ${dmg} 데미지!`, () => {
    if (battleState.enemy.hp <= 0) { endBattle(true); return; }
    enemyTurn();
  });
}

function playerSkill() {
  if (!canActInBattle()) return;
  if (battleState.skillUsesLeft <= 0) return;
  setButtons(false);
  battleState.skillUsesLeft -= 1;
  updateSkillButtonLabel();
  const dmg = calcDamage(battleState.player.attack * 1.8) | 0;
  battleState.enemy.hp = Math.max(0, battleState.enemy.hp - dmg);
  shake("enemy");
  printLog(`${battleState.player.name}의 필살기! ${dmg} 데미지!`, () => {
    if (battleState.enemy.hp <= 0) { endBattle(true); return; }
    enemyTurn();
  });
}

function playerRun() {
  if (!battleState.active || battleState.role !== "host") return;
  setButtons(false);
  printLog("도망쳤다!", () => endBattle(null));
}

function playerTeamAttack() {
  if (!canActInBattle() || !battleState.ally) return;
  setButtons(false);
  const dmg = calcDamage(battleState.player.attack + Math.floor(battleState.ally.attack * 0.8));
  battleState.enemy.hp = Math.max(0, battleState.enemy.hp - dmg);
  shake("enemy");
  printLog(`${battleState.player.name}와 ${battleState.ally.name}의 협동 공격! ${dmg} 데미지!`, () => {
    if (battleState.enemy.hp <= 0) { endBattle(true); return; }
    enemyTurn();
  });
}

function enemyTurn() {
  const dmg = calcDamage(battleState.enemy.attack);
  battleState.player.hp = Math.max(0, battleState.player.hp - dmg);
  shake("player");
  printLog(`${battleState.enemy.name}의 공격! ${dmg} 데미지!`, () => {
    if (battleState.player.hp <= 0) { endBattle(false); return; }
    startPlayerTurn();
  });
}

function startPlayerTurn() {
  if (!battleState.active || battleState.role !== "host") return;
  battleState.playerTurn = true;
  if (battleState.pendingAlly && !battleState.ally) {
    activatePendingAlly();
    return;
  }
  setButtons(true);
}

function calcDamage(base) {
  return Math.max(1, (base + Math.floor(Math.random() * 4) - 1) | 0);
}

function shake(target) {
  battleState.animShake = { target, until: performance.now() + 400 };
}

// ============================================================
//  전투 종료
// ============================================================
function endBattle(won) {
  battleState.active = false;
  hideBattleWaitingPanel();
  broadcastBattleEvent("ended", { won });
  syncStarterHpAfterBattle(won);
  const msg = won === true  ? "승리했다! 🎉"
            : won === false ? "쓰러졌다... 😢"
            : "도망쳤다!";
  printLog(msg, () => {
    if (won === true && state.starter) {
      const rewards = awardStarterExp(battleState.enemy?.expReward || 0);
      showBattleMessages(rewards, finalizeBattleEnd.bind(null, won));
      return;
    }
    finalizeBattleEnd(won);
  });
}

function finalizeBattleEnd(won) {
  setTimeout(() => {
    battleScreen.style.display = "none";
    state.phase = "game";
    if (battleState.onEnd) battleState.onEnd(won);
  }, 800);
}

function showBattleMessages(messages, done) {
  const queue = (messages || []).filter(Boolean);
  if (!queue.length) {
    done();
    return;
  }

  const [first, ...rest] = queue;
  printLog(first, () => showBattleMessages(rest, done));
}

function syncStarterHpAfterBattle(won) {
  if (!state.starter || !battleState.player) return;
  state.starter = normalizeStarter(state.starter);
  const stats = getStarterStats(state.starter);
  const nextHp = won === false
    ? Math.max(1, battleState.player.hp)
    : Math.max(1, Math.min(battleState.player.hp, stats.maxHp));
  state.starter.currentHp = nextHp;
  saveGame();
  updateHUD();
}

// ============================================================
//  로그 출력 (딜레이)
// ============================================================
function printLog(text, callback) {
  if (printLog._timer) clearInterval(printLog._timer);
  bLog.textContent = "";
  let i = 0;
  printLog._timer = setInterval(() => {
    bLog.textContent += text[i++];
    if (i >= text.length) {
      clearInterval(printLog._timer);
      printLog._timer = null;
      if (callback) setTimeout(callback, 600);
    }
  }, 30);
}
printLog._timer = null;

function setButtons(enabled) {
  const canHostAct = enabled && battleState.role === "host";
  [bBtnAttack, bBtnSkill, bBtnRun].forEach(b => b.disabled = !canHostAct);
  bBtnTeam.disabled = !canHostAct || !battleState.ally;
  if (enabled && battleState.skillUsesLeft <= 0) bBtnSkill.disabled = true;
}

function updateSkillButtonLabel() {
  bBtnSkill.textContent = `✨ 필살기 ${battleState.skillUsesLeft}/${MAX_SKILL_USES}`;
}

// ── 버튼 이벤트 ──
bBtnAttack.addEventListener("click", playerAttack);
bBtnSkill.addEventListener("click",  playerSkill);
bBtnTeam.addEventListener("click",   playerTeamAttack);
bBtnRun.addEventListener("click",    playerRun);
battleJoinConfirm.addEventListener("click", requestBattleJoin);

function canActInBattle() {
  return battleState.playerTurn && battleState.active && battleState.role === "host";
}

function sanitizeBattleMonster(mon) {
  if (!mon) return null;
  return {
    name: mon.name,
    level: mon.level || null,
    hp: mon.hp,
    maxHp: mon.maxHp,
    attack: mon.attack,
    emoji: mon.emoji || "⚡",
    color: mon.color || "#FFD700",
    expReward: mon.expReward || 0,
    battleIntro: mon.battleIntro || null,
  };
}

function getLocalBattleMember() {
  const starter = normalizeStarter(state.starter) || createStarterFromTemplate(getStarterTemplate("fire"));
  const stats = getStarterStats(starter);
  return {
    name: state.playerName || starter.name || "팀원",
    level: stats.level,
    hp: Math.max(1, Math.min(starter.currentHp ?? stats.maxHp, stats.maxHp)),
    maxHp: stats.maxHp,
    attack: stats.attack,
    emoji: starter.emoji || "⚡",
    color: starter.color || "#FFD700",
  };
}

function broadcastBattleEvent(action, payload = {}) {
  if (state.mode !== "multi" || typeof sendBattleEvent !== "function") return false;
  return sendBattleEvent({
    action,
    battleId: battleState.battleId,
    ...payload,
  });
}

function showBattleWaitingPanel(title, text, showJoinButton) {
  battleWaitingTitle.textContent = title;
  battleWaitingText.textContent = text;
  battleJoinConfirm.style.display = showJoinButton ? "inline-block" : "none";
  battleWaitingPanel.style.display = "block";
}

function hideBattleWaitingPanel() {
  battleWaitingPanel.style.display = "none";
}

function requestBattleJoin() {
  const offer = battleState.remoteOffer;
  if (!offer || state.mode !== "multi") return;
  startBattle(offer.enemy, null, {
    role: "joiner",
    battleId: offer.battleId,
    hostPlayerId: offer.hostPlayerId,
  });
  showBattleWaitingPanel("전투 참여 대기 중", "다음 유저 턴에 입장합니다.", false);
  sendBattleEvent({
    action: "join_request",
    battleId: offer.battleId,
    member: getLocalBattleMember(),
    requesterName: state.playerName,
  });
}

function activatePendingAlly() {
  const ally = battleState.pendingAlly.member;
  battleState.ally = ally;
  battleState.pendingAlly = null;
  broadcastBattleEvent("ally_joined", {
    ally,
    enemy: sanitizeBattleMonster(battleState.enemy),
  });
  printLog(`${ally.name}이(가) 전투에 합류했다!`, () => setButtons(true));
}

function showRemoteBattleOffer(senderId, payload) {
  if (battleState.active || state.phase !== "game") return;
  battleState.remoteOffer = {
    hostPlayerId: senderId,
    battleId: payload.battleId,
    enemy: payload.enemy,
    hostName: payload.hostName || "팀원",
  };
  battleScreen.style.display = "flex";
  showBattleWaitingPanel("팀원이 전투 중", `${battleState.remoteOffer.hostName}의 전투에 참여할 수 있습니다.`, true);
  bLog.textContent = "전투 참여를 선택하면 다음 유저 턴까지 대기합니다.";
  setButtons(false);
}

function handleBattleEvent(senderId, payload) {
  if (!payload || state.mode !== "multi") return;

  if (payload.action === "started") {
    showRemoteBattleOffer(senderId, payload);
    return;
  }

  if (payload.action === "join_request") {
    if (!battleState.active || battleState.role !== "host") return;
    if (payload.battleId !== battleState.battleId) return;
    if (battleState.ally || battleState.pendingAlly) {
      broadcastBattleEvent("join_closed", { targetPlayerId: senderId });
      return;
    }
    battleState.pendingAlly = {
      playerId: senderId,
      member: payload.member || { ...getLocalBattleMember(), name: payload.requesterName || "팀원" },
    };
    broadcastBattleEvent("join_pending", {
      targetPlayerId: senderId,
      enemy: sanitizeBattleMonster(battleState.enemy),
    });
    printLog(`${battleState.pendingAlly.member.name}이(가) 참여를 준비 중입니다.`);
    return;
  }

  if (payload.action === "join_pending") {
    if (!battleState.active || payload.battleId !== battleState.battleId) return;
    showBattleWaitingPanel("전투 참여 대기 중", "다음 유저 턴에 입장합니다.", false);
    return;
  }

  if (payload.action === "ally_joined") {
    if (!battleState.active || payload.battleId !== battleState.battleId) return;
    battleState.ally = payload.ally || battleState.player;
    if (payload.enemy) battleState.enemy = { ...battleState.enemy, ...payload.enemy };
    hideBattleWaitingPanel();
    printLog(`${battleState.ally.name}이(가) 전투에 입장했습니다.`);
    setButtons(false);
    return;
  }

  if (payload.action === "join_closed") {
    if (!battleState.active || payload.battleId !== battleState.battleId) return;
    showBattleWaitingPanel("전투 참여 불가", "이미 다른 팀원이 전투에 참여 중입니다.", false);
    return;
  }

  if (payload.action === "ended") {
    if (!battleState.active && battleState.remoteOffer?.battleId === payload.battleId) {
      hideBattleWaitingPanel();
      battleScreen.style.display = "none";
      battleState.remoteOffer = null;
      return;
    }
    if (!battleState.active || payload.battleId !== battleState.battleId) return;
    battleState.active = false;
    hideBattleWaitingPanel();
    printLog("팀원의 전투가 종료되었습니다.", () => {
      battleScreen.style.display = "none";
      state.phase = "game";
    });
  }
}

// ============================================================
//  외부에서 전투 시작하는 헬퍼
// ============================================================
function startWildBattle(onEnd) {
  const mon = WILD_MONSTERS[Math.floor(Math.random() * WILD_MONSTERS.length)];
  startBattle({ ...mon }, onEnd);
}

function startTrainerBattle(trainerId, onEnd) {
  const mon = TRAINER_MONSTERS[trainerId] || WILD_MONSTERS[0];
  startBattle({ ...mon, battleIntro: `${mon.name}이(가) 승부를 걸어왔다!` }, onEnd);
}

return {
  startBattle,
  startWildBattle,
  startTrainerBattle,
  handleBattleEvent,
};
}
