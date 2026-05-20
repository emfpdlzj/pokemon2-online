// ============================================================
//  battle.js - 턴제 전투 시스템
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
};

// ── DOM ──
const battleScreen  = document.getElementById("battle-screen");
const bCanvasEl     = document.getElementById("battle-canvas");
const bCtx          = bCanvasEl.getContext("2d");
const bLog          = document.getElementById("battle-log");
const bBtnAttack    = document.getElementById("b-attack");
const bBtnSkill     = document.getElementById("b-skill");
const bBtnRun       = document.getElementById("b-run");
const MAX_SKILL_USES = 7;

const B_W = 640, B_H = 320;
bCanvasEl.width  = B_W;
bCanvasEl.height = B_H;

// ============================================================
//  전투 시작
// ============================================================
function startBattle(enemyData, onEnd) {
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

  state.phase = "battle";
  battleScreen.style.display = "flex";
  updateSkillButtonLabel();
  setButtons(false);

  const introText = battleState.enemy.battleIntro || `야생의 ${battleState.enemy.name}이(가) 나타났다!`;
  printLog(introText, () => {
    printLog(`${battleState.player.name}, 싸워라!`, () => setButtons(true));
  });

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
  drawStatusPanel(bCtx, battleState.player, 24, B_H - 104, 240, true);
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
  if (!battleState.playerTurn || !battleState.active) return;
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
  if (!battleState.playerTurn || !battleState.active) return;
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
  if (!battleState.active) return;
  setButtons(false);
  printLog("도망쳤다!", () => endBattle(null));
}

function enemyTurn() {
  const dmg = calcDamage(battleState.enemy.attack);
  battleState.player.hp = Math.max(0, battleState.player.hp - dmg);
  shake("player");
  printLog(`${battleState.enemy.name}의 공격! ${dmg} 데미지!`, () => {
    if (battleState.player.hp <= 0) { endBattle(false); return; }
    setButtons(true);
  });
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
  bLog.textContent = "";
  let i = 0;
  const interval = setInterval(() => {
    bLog.textContent += text[i++];
    if (i >= text.length) {
      clearInterval(interval);
      if (callback) setTimeout(callback, 600);
    }
  }, 30);
}

function setButtons(enabled) {
  [bBtnAttack, bBtnSkill, bBtnRun].forEach(b => b.disabled = !enabled);
  if (enabled && battleState.skillUsesLeft <= 0) bBtnSkill.disabled = true;
}

function updateSkillButtonLabel() {
  bBtnSkill.textContent = `✨ 필살기 ${battleState.skillUsesLeft}/${MAX_SKILL_USES}`;
}

// ── 버튼 이벤트 ──
bBtnAttack.addEventListener("click", playerAttack);
bBtnSkill.addEventListener("click",  playerSkill);
bBtnRun.addEventListener("click",    playerRun);

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
