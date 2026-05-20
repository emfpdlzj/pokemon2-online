// ============================================================
//  LLM 설정 (여기만 수정하면 다른 API로 교체 가능)
// ============================================================
const API_KEY    = "AIzaSyDLstOJ2l2Itoi5nzxAmDHzqE9yjNub1kY";
const API_URL    = "https://generativelanguage.googleapis.com/v1beta/models";
const MODEL_NAME = "gemini-2.0-flash";

const RIVAL_SYSTEM_PROMPT = `
너는 포켓몬풍 RPG 게임 속 플레이어의 소꿉친구 '리벨'이야.
성격: 밝고 자신감 있고 살짝 경쟁심 있음.
말투: 짧고 직접적. 반말. 항상 1~2문장으로만 대답.
제약:
- 첫 번째 마을과 연구소 주변 이야기만 알고 있음
- 미래 스토리 스포일러 금지
- 현대 인터넷 용어 사용 금지
- 자신이 AI라고 말하면 안 됨
- 게임 세계관 밖 이야기 금지
응답 예시: "흥, 나보다 먼저 강해질 생각은 하지 마!", "앞쪽 길은 조심해. 괜히 겁먹은 건 아니거든!"
`.trim();

const BINNA_SYSTEM_PROMPT = `
너는 포켓몬풍 RPG 게임 속 플레이어의 소꿉친구 '빛나'야.
성격: 다정하고 활발하며 응원해 주는 편이야.
말투: 짧고 자연스러운 반말. 항상 1~2문장으로만 대답.
제약:
- 첫 번째 마을, 박사 연구소, 1번 도로, 나팔꽃마을 초반 구간만 알고 있음
- 미래 스토리 스포일러 금지
- 현대 인터넷 용어 사용 금지
- 자신이 AI라고 말하면 안 됨
- 게임 세계관 밖 이야기 금지
응답 예시: "와, 벌써 여기까지 왔네! 역시 너답다.", "또 만났네! 같이 마을도 둘러볼래?"
`.trim();

// 선택지 생성용 프롬프트
const CHOICES_SYSTEM_PROMPT = `
너는 포켓몬풍 RPG 게임의 대화 선택지 생성기야.
플레이어는 막 스타팅 몬스터를 받고 모험을 시작한 초보 트레이너야.
배경: 시작 마을, 박사 연구소, 1번 도로, 나팔꽃마을 초반 구간.

상대 캐릭터의 대사를 받으면, 플레이어가 할 수 있는 자연스러운 짧은 대답 3개를 생성해.
선택지는 반드시 아래 조건을 따라야 해:
- 상대 대사에 직접 반응해야 함. 뜬금없는 주제 전환 금지
- 상대가 질문하면 답하거나 되묻는 선택지를 포함
- 상대가 도발해도 플레이어는 시비 걸거나 공격적으로 말하지 않음
- 플레이어 선택지는 친근함 / 침착함 / 호기심 위주로 생성
- 직전 대사에 없는 정보(체육관, 전설의 몬스터, 먼 미래 지역 등) 추가 금지
- 포켓몬 세계관 줄거리와 연관 (예: 스타팅 몬스터, 모험, 마을, 트레이너 배틀, 박사)
- 각각 다른 감정/태도 (예: 자신감 있는 / 궁금한 / 도전적인)
- 10자 이내, 반말, 게임 캐릭터 말투
- 현대 인터넷 용어 금지

반드시 아래 JSON 형식으로만 응답해. 다른 텍스트 없이:
{"choices":["선택지1","선택지2","선택지3"]}

예시:
{"choices":["내 파트너가 더 강해!","박사님한테 배웠거든.","같이 강해지자!"]}
`.trim();

const RIVAL_CHOICE_POOLS = {
  question: ["왜 그렇게 말해?", "어디로 갈 건데?", "박사님이 말했어?", "같이 가볼래?", "정말 괜찮아?", "뭘 본 거야?"],
  taunt: ["알겠어.", "같이 가볼래?", "나도 열심히 할게.", "먼저 보고 올게.", "정말 괜찮아?", "조심해서 갈게."],
  warning: ["조심할게.", "넌 안 무서워?", "같이 가자.", "그래도 갈래.", "뭐가 있는 거야?", "알겠어."],
  starter: ["내 파트너야.", "잘 어울리지?", "금방 강해질걸.", "네 몬스터는 어때?", "박사님이 주셨어.", "멋지지?"],
  greeting: ["나도 방금 왔어.", "뭐하고 있었어?", "같이 얘기할래?", "먼저 보고 있었어?", "무슨 일 있어?", "반가워."],
  generic: ["그렇구나.", "나도 그렇게 생각해.", "같이 가보자.", "왜 그렇게 말해?", "알겠어.", "조심해서 갈게."],
};

function classifyRivalMessage(message) {
  const text = (message || "").replace(/\s+/g, " ").trim();
  if (!text) return "generic";
  if (/(파이리|꼬부기|이상해씨|파트너|스타터|불 타입|물 타입|풀 타입|박사님한테 받)/.test(text)) return "starter";
  if (/(조심|위험|겁|무서|괜히|주의)/.test(text)) return "warning";
  if (/(먼저 강해|늦었|내가 더 강|안 통할 거|두고 봐|이길 거)/.test(text)) return "taunt";
  if (/[?？]|어디|왜|뭐|정말|같이 .*갈래|궁금/.test(text)) return "question";
  if (/(안녕|왔네|왔구나|반갑|기다리고)/.test(text)) return "greeting";
  return "generic";
}

function normalizeChoiceText(choice) {
  return String(choice || "")
    .replace(/["'`]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

function isValidRivalChoice(choice, type) {
  if (!choice) return false;
  if (choice.length > 14) return false;
  if (/[{}[\]:]/.test(choice)) return false;
  if (/(체육관|전설|포켓몬리그|인터넷|유튜브|AI|메타)/i.test(choice)) return false;
  if (/(두고 봐|이길 거야|안 통할 거야|겁 안 나|비켜|시끄러|웃기네|덤벼)/.test(choice)) return false;
  if (type === "warning" && !/(조심|같이|갈래|알겠|무서|뭐가|그래도)/.test(choice)) return false;
  if (type === "starter" && !/(파트너|박사|강해|멋|어울|몬스터)/.test(choice)) return false;
  if (type === "taunt" && !/(알겠|같이|열심히|먼저|정말|조심)/.test(choice)) return false;
  if (type === "question" && !(/[?？]/.test(choice) || /(왜|어디|뭐|같이|정말)/.test(choice))) return false;
  return true;
}

function buildFallbackChoices(rivalMessage) {
  const type = classifyRivalMessage(rivalMessage);
  const pool = RIVAL_CHOICE_POOLS[type] || RIVAL_CHOICE_POOLS.generic;
  return pool.slice(0, 3);
}

function sanitizeRivalChoices(rawChoices, rivalMessage) {
  const type = classifyRivalMessage(rivalMessage);
  const pool = [...(RIVAL_CHOICE_POOLS[type] || []), ...RIVAL_CHOICE_POOLS.generic];
  const unique = [];

  (rawChoices || []).forEach(choice => {
    const cleaned = normalizeChoiceText(choice);
    if (!isValidRivalChoice(cleaned, type)) return;
    if (unique.includes(cleaned)) return;
    unique.push(cleaned);
  });

  pool.forEach(choice => {
    if (unique.length >= 3) return;
    if (unique.includes(choice)) return;
    unique.push(choice);
  });

  return unique.slice(0, 3);
}

// ── LLM 요청 함수 (Gemini API) ──
async function callLLMWithPrompt(systemPrompt, userMessage) {
  const url = `${API_URL}/${MODEL_NAME}:generateContent?key=${API_KEY}`;
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      system_instruction: { parts: [{ text: systemPrompt }] },
      contents: [{ role: "user", parts: [{ text: userMessage }] }],
      generationConfig: { maxOutputTokens: 80 },
    }),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

// ── 응답 파싱 함수 (Gemini) ──
function parseLLMResponse(data) {
  return data?.candidates?.[0]?.content?.parts?.[0]?.text?.trim() ?? null;
}

const FALLBACK_REPLIES = [
  "나중에 다시 말 걸어줘!",
  "지금은 좀 바빠. 이따 얘기하자!",
  "음... 잘 모르겠는데, 나중에 물어봐.",
];

function sanitizeCharacterReply(reply, fallback) {
  const text = String(reply || "").replace(/\s+/g, " ").trim();
  if (!text) return fallback;
  if (text.length > 60) return fallback;
  if (/[^\u3131-\u318E\uAC00-\uD7A3a-zA-Z0-9\s?!,.~"'():\-]/.test(text)) return fallback;
  const sentenceCount = (text.match(/[?!\.]/g) || []).length;
  if (sentenceCount > 3) return fallback;
  return text;
}

async function getRivalReply(userMessage) {
  try {
    const data = await callLLMWithPrompt(RIVAL_SYSTEM_PROMPT, userMessage);
    return sanitizeCharacterReply(
      parseLLMResponse(data),
      FALLBACK_REPLIES[Math.floor(Math.random() * FALLBACK_REPLIES.length)]
    );
  } catch {
    return FALLBACK_REPLIES[Math.floor(Math.random() * FALLBACK_REPLIES.length)];
  }
}

async function getBinnaReply(userMessage) {
  try {
    const data = await callLLMWithPrompt(BINNA_SYSTEM_PROMPT, userMessage);
    return sanitizeCharacterReply(parseLLMResponse(data), "또 얘기하자!");
  } catch {
    return "또 얘기하자!";
  }
}

// 선택지 3개 생성
async function getRivalChoices(rivalMessage) {
  try {
    const url = `${API_URL}/${MODEL_NAME}:generateContent?key=${API_KEY}`;
    const res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        system_instruction: { parts: [{ text: CHOICES_SYSTEM_PROMPT }] },
        contents: [{ role: "user", parts: [{ text: rivalMessage }] }],
        generationConfig: { maxOutputTokens: 100 },
      }),
    });
    const data = await res.json();
    const raw = parseLLMResponse(data) || "";
    const json = JSON.parse(raw.replace(/```json|```/g, "").trim());
    if (Array.isArray(json.choices)) {
      const cleaned = sanitizeRivalChoices(json.choices, rivalMessage);
      if (cleaned.length === 3) return cleaned;
    }
  } catch {}
  return buildFallbackChoices(rivalMessage);
}

// ============================================================
//  게임 상수
// ============================================================
const CANVAS_W = 640;
const CANVAS_H = 480;
const TILE     = 32;
const MOVE_DELAY = 0; // ms, 다음 이동 입력 허용 간격
const MOVE_DURATION = 220; // ms, 한 타일 실제 이동 시간
const NPC_MOVE_DURATION = 220; // ms, NPC 한 타일 이동 시간
const NPC_STEP_DELAY = NPC_MOVE_DURATION + 30; // ms, 다음 NPC 이동까지 대기

// 렌더링용 픽셀 좌표
let renderX = 0, renderY = 0;

const moveState = {
  active: false,
  fromX: 0,
  fromY: 0,
  toX: 0,
  toY: 0,
  targetX: 0,
  targetY: 0,
  startTs: 0,
  arrived: false,
};

function easeOutQuad(t) {
  return 1 - (1 - t) * (1 - t);
}

function easeInOutQuad(t) {
  return t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
}

const STARTERS = [
  { id:"fire",  name:"파이리",  type:"불 타입",  color:"#FF4500", emoji:"🔥", baseAttack:8, baseHp:35 },
  { id:"water", name:"꼬부기",  type:"물 타입",  color:"#4169E1", emoji:"💧", baseAttack:7, baseHp:37 },
  { id:"grass", name:"이상해씨", type:"풀 타입", color:"#32CD32", emoji:"🌿", baseAttack:7, baseHp:36 },
];

function getStarterTemplate(id) {
  return STARTERS.find(starter => starter.id === id) || STARTERS[0];
}

function expToNextLevel(level) {
  return 12 + (level - 1) * 8;
}

function normalizeStarter(starter) {
  if (!starter) return null;
  const template = getStarterTemplate(starter.id);
  const level = Math.max(1, starter.level || 1);
  const exp = Math.max(0, starter.exp || 0);
  const maxHp = template.baseHp + (level - 1) * 5;
  const currentHp = Math.min(maxHp, Math.max(0, starter.currentHp ?? maxHp));
  return {
    ...template,
    ...starter,
    level,
    exp,
    currentHp,
  };
}

function getStarterStats(starter) {
  const mon = normalizeStarter(starter);
  if (!mon) return null;
  return {
    level: mon.level,
    attack: mon.baseAttack + (mon.level - 1) * 2,
    maxHp: mon.baseHp + (mon.level - 1) * 5,
    nextExp: expToNextLevel(mon.level),
  };
}

function createStarterFromTemplate(template) {
  return normalizeStarter({
    ...template,
    level: 1,
    exp: 0,
    currentHp: template.baseHp,
  });
}

function awardStarterExp(amount) {
  if (!state.starter || amount <= 0) return [];
  state.starter = normalizeStarter(state.starter);
  const messages = [`${state.starter.name}은(는) 경험치 ${amount}를 얻었다!`];

  state.starter.exp += amount;
  while (state.starter.exp >= expToNextLevel(state.starter.level)) {
    state.starter.exp -= expToNextLevel(state.starter.level);
    state.starter.level += 1;
    const stats = getStarterStats(state.starter);
    state.starter.currentHp = stats.maxHp;
    messages.push(`${state.starter.name}의 레벨이 ${state.starter.level}(으)로 올랐다!`);
  }

  saveGame();
  updateHUD();
  return messages;
}

// ============================================================
//  게임 상태
// ============================================================
const DEFAULT_EVENTS = {
  professorMet: false,
  starterChosen: false,
  rivalMet: false,
  rivalEventTriggered: false,
  binnaCongrats: false,
  binnaTown2Met: false,
  town2Visited: false,
};

const state = {
  playerName: "주인공",
  starter: null,          // { id, name, type, color }
  currentMap: "hometown",
  px: 9, py: 9,           // 타일 좌표
  events: { ...DEFAULT_EVENTS }, // 완료된 이벤트 플래그
  mode: "single",
  roomId: null,
  phase: "title",         // title | game | cutscene
};

// ============================================================
//  DOM 참조
// ============================================================
const canvas      = document.getElementById("game-canvas");
const ctx         = canvas.getContext("2d");
const hudEl       = document.getElementById("hud");
const hudMap      = document.getElementById("hud-map");
const hudStarter  = document.getElementById("hud-starter");
const dialogueBox = document.getElementById("dialogue-box");
const dlgSpeaker  = document.getElementById("dialogue-speaker");
const dlgText     = document.getElementById("dialogue-text");
const dlgNext     = document.getElementById("dialogue-next");
const choicesEl   = document.getElementById("choices");
const starterOverlay = document.getElementById("starter-overlay");
const fadeOverlay = document.getElementById("fade-overlay");
const startScreen = document.getElementById("start-screen");
const gameWrapper = document.getElementById("game-wrapper");
const mainMenu = document.getElementById("main-menu");
const menuStatus = document.getElementById("menu-status");
const saveMenuPanel = document.getElementById("save-menu-panel");
const multiMenuPanel = document.getElementById("multi-menu-panel");
const settingsPanel = document.getElementById("settings-panel");
const saveSlotsEl = document.getElementById("save-slots");
const roomListEl = document.getElementById("room-list");
const serverUrlInput = document.getElementById("server-url-input");

canvas.width  = CANVAS_W;
canvas.height = CANVAS_H;

// ============================================================
//  대화 시스템
// ============================================================
let dlgQueue    = [];
let dlgIndex    = 0;
let dlgSpeakerName = "";
let dlgCallback = null;
let inDialogue  = false;
let dlgWaitingAI = false;

function formatDialogueLine(line) {
  return String(line ?? "")
    .replaceAll("{playerName}", state.playerName || "주인공");
}

function showDialogue(lines, speakerName, callback) {
  dlgQueue       = (lines || []).map(formatDialogueLine);
  dlgIndex       = 0;
  dlgSpeakerName = speakerName || "";
  dlgCallback    = callback || null;
  inDialogue     = true;
  dialogueBox.style.display = "block";
  choicesEl.style.display   = "none";
  renderDialogueLine();
}

function renderDialogueLine() {
  dlgSpeaker.textContent = dlgSpeakerName;
  dlgNext.style.display = "none";
  // 타이핑 효과
  const text = dlgQueue[dlgIndex];
  dlgText.textContent = "";
  let i = 0;
  if (renderDialogueLine._timer) clearInterval(renderDialogueLine._timer);
  renderDialogueLine._timer = setInterval(() => {
    dlgText.textContent += text[i++];
    if (i >= text.length) {
      clearInterval(renderDialogueLine._timer);
      dlgNext.style.display = "block";
    }
  }, 28);
}
renderDialogueLine._timer = null;

function advanceDialogue() {
  if (!inDialogue) return;
  if (dlgWaitingAI) return; // AI 대기 중 Enter 무시
  dlgIndex++;
  if (dlgIndex < dlgQueue.length) {
    renderDialogueLine();
  } else {
    const cb = dlgCallback;
    dlgCallback = null;
    closeDialogue();
    if (cb) cb();
  }
}

function closeDialogue() {
  inDialogue = false;
  dialogueBox.style.display = "none";
  choicesEl.style.display   = "none";
}

function showChoices(options) {
  // options: [{ label, callback }]
  choicesEl.innerHTML = "";
  choicesEl.style.display = "flex";
  options.forEach(opt => {
    const btn = document.createElement("button");
    btn.className   = "choice-btn";
    btn.textContent = opt.label;
    btn.onclick = () => {
      choicesEl.style.display = "none";
      opt.callback();
    };
    choicesEl.appendChild(btn);
  });
}

function fadeScreen(toBlack = true) {
  return new Promise(resolve => {
    fadeOverlay.style.opacity = toBlack ? "1" : "0";
    setTimeout(resolve, 440);
  });
}

function healStarterFull() {
  if (!state.starter) return;
  state.starter = normalizeStarter(state.starter);
  const stats = getStarterStats(state.starter);
  state.starter.currentHp = stats.maxHp;
  saveGame();
  updateHUD();
}

// ============================================================
//  스타터 선택 오버레이
// ============================================================
function openStarterOverlay(callback) {
  starterOverlay.style.display = "flex";
  const cards = document.getElementById("starter-cards");
  cards.innerHTML = "";
  STARTERS.forEach(s => {
    const card = document.createElement("div");
    card.className = "starter-card";
    card.innerHTML = `
      <div class="starter-icon" style="background:${s.color}"></div>
      <div class="s-name">${s.name}</div>
      <div class="s-type">${s.type}</div>
    `;
    card.onclick = () => {
      starterOverlay.style.display = "none";
      callback(s);
    };
    cards.appendChild(card);
  });
}

// ============================================================
//  LLM 채팅 UI
// ============================================================
//  HUD 업데이트
// ============================================================
function updateHUD() {
  const mapData = MAPS[state.currentMap];
  hudMap.textContent = `📍 ${mapData.name}`;
  if (state.starter) {
    state.starter = normalizeStarter(state.starter);
    const stats = getStarterStats(state.starter);
    hudStarter.textContent = `${state.starter.emoji} ${state.starter.name} Lv.${stats.level} EXP ${state.starter.exp}/${stats.nextExp}`;
  } else {
    hudStarter.textContent = "파트너: 없음";
  }
}

// ============================================================
//  타일 렌더러 - 고품질 픽셀 드로잉
// ============================================================

// NPC 픽셀 스타일 정의
const NPC_STYLES = {
  default:            { skin:"#FFDEAD", shirt:"#4682B4", pants:"#2F4F4F", hair:"#4a3000" },
  mom:                { skin:"#FFDEAD", shirt:"#FF69B4", pants:"#9370DB", hair:"#8B4513" },
  mom_home:           { skin:"#FFDEAD", shirt:"#FF69B4", pants:"#9370DB", hair:"#8B4513" },
  oldman:             { skin:"#DEB887", shirt:"#708090", pants:"#556B2F", hair:"#C0C0C0", accessory:"cane" },
  professor:          { skin:"#FFDEAD", shirt:"#FFFFFF", pants:"#4169E1", hair:"#C0C0C0", hat:"#FFFFFF" },
  starter_table:      { skin:"#FFD700", shirt:"#FFD700", pants:"#FFA500", hair:"#FF6600" },
  trainer1:           { skin:"#FFDEAD", shirt:"#FF4500", pants:"#1a1a2e", hair:"#8B0000", hat:"#CC0000" },
  rival_route:        { skin:"#FFDEAD", shirt:"#00CED1", pants:"#1a1a2e", hair:"#00CED1" },
  binna_hometown:     { skin:"#FFDEAD", shirt:"#F6C2D9", pants:"#3A4A7A", hair:"#1F2C5C" },
  binna_town2:        { skin:"#FFDEAD", shirt:"#F6C2D9", pants:"#3A4A7A", hair:"#1F2C5C" },
  town2_villager1:    { skin:"#FFDEAD", shirt:"#FF8C00", pants:"#556B2F", hair:"#4a3000" },
  town2_villager2:    { skin:"#DEB887", shirt:"#9370DB", pants:"#696969", hair:"#C0C0C0", accessory:"cane" },
  town2_villager3:    { skin:"#FFDEAD", shirt:"#20B2AA", pants:"#2244AA", hair:"#1a1a2e" },
  town2_construction: { skin:"#FFDEAD", shirt:"#FF6347", pants:"#FF8C00", hair:"#4a3000", hat:"#FFD700" },
  house1_npc:         { skin:"#FFDEAD", shirt:"#DEB887", pants:"#8B4513", hair:"#4a3000" },
  house2_npc:         { skin:"#DEB887", shirt:"#9370DB", pants:"#696969", hair:"#C0C0C0", accessory:"cane" },
  house3_npc:         { skin:"#FFDEAD", shirt:"#20B2AA", pants:"#2244AA", hair:"#1a1a2e" },
  shopkeeper:         { skin:"#FFDEAD", shirt:"#FF8C00", pants:"#4a3000", hair:"#4a3000", hat:"#FF8C00" },
  nurse:              { skin:"#FFDEAD", shirt:"#FFFFFF", pants:"#FF69B4", hair:"#FF69B4", hat:"#FFFFFF" },
  guard:              { skin:"#FFDEAD", shirt:"#708090", pants:"#2F4F4F", hair:"#1a1a2e", hat:"#556B2F" },
};

// NPC 이미지 캐시 - 미사용 (픽셀 드로잉 전용)
// 주인공 방향별 이미지 - 미사용 (픽셀 드로잉 사용)
// 라이벌 방향별 이미지 - 미사용
// 스타터 이미지 - 미사용

let playerDir = "down"; // up | down | left | right

function loadNpcImages(map) {} // 미사용

// ── 타일별 상세 드로잉 함수 ──
function drawTile(ctx, tileType, sx, sy, map, tx, ty) {
  const T = TILE;
  switch (tileType) {
    case 0: drawGrass(ctx, sx, sy, map); break;
    case 1: drawWall(ctx, sx, sy, map, tx, ty); break;
    case 2: drawDoor(ctx, sx, sy);       break;
    case 3: drawPath(ctx, sx, sy);       break;
    case 4: drawWater(ctx, sx, sy);      break;
    case 5: drawDeco(ctx, sx, sy);       break;
    case 6: drawTallGrass(ctx, sx, sy);  break;
    default:
      ctx.fillStyle = map.bgColor;
      ctx.fillRect(sx, sy, T, T);
  }
}

function drawTallGrass(ctx, sx, sy) {
  const T = TILE;
  // 짙은 초록 베이스
  ctx.fillStyle = "#2E8B57";
  ctx.fillRect(sx, sy, T, T);
  // 풀잎 (밝은 색)
  ctx.fillStyle = "#3CB371";
  ctx.fillRect(sx+2, sy+2, T-4, T-4);
  // 풀잎 디테일
  ctx.fillStyle = "#228B22";
  const blades = [[4,20],[8,14],[13,22],[18,16],[22,20],[10,8],[20,10]];
  blades.forEach(([x,y]) => {
    ctx.fillRect(sx+x,   sy+y-8, 2, 8);
    ctx.fillRect(sx+x-2, sy+y-5, 2, 5);
    ctx.fillRect(sx+x+2, sy+y-6, 2, 6);
  });
  // 반짝임
  ctx.fillStyle = "rgba(255,255,255,0.1)";
  ctx.fillRect(sx+3, sy+3, 4, 3);
}

function drawGrass(ctx, sx, sy, map) {
  const T = TILE;
  // 베이스
  ctx.fillStyle = map.tileColors[0];
  ctx.fillRect(sx, sy, T, T);
  // 밝은 하이라이트 패치
  ctx.fillStyle = "rgba(255,255,255,0.07)";
  ctx.fillRect(sx + 2, sy + 2, 6, 4);
  ctx.fillRect(sx + 18, sy + 14, 5, 3);
  // 풀잎 디테일
  ctx.strokeStyle = "rgba(0,80,0,0.25)";
  ctx.lineWidth = 1;
  [[4,22],[10,18],[18,24],[24,20],[8,10],[22,8]].forEach(([x,y]) => {
    ctx.beginPath();
    ctx.moveTo(sx+x, sy+y);
    ctx.lineTo(sx+x-2, sy+y-5);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(sx+x, sy+y);
    ctx.lineTo(sx+x+2, sy+y-5);
    ctx.stroke();
  });
}

function drawWall(ctx, sx, sy, map, tx, ty) {
  const T = TILE;

  // building 맵은 건물 블록만 벽돌로 그리고, 외곽 벽은 기본적으로 나무/풀 스타일로 처리한다.
  const building = map.buildings?.find(b =>
    tx >= b.x && tx < b.x + b.w && ty >= b.y && ty < b.y + b.h
  );

  const outerStyle = map.outerWallStyle || (map.wallStyle === "building" ? "tree" : (map.wallStyle || "tree"));
  const style = building?.style || (building ? "building" : outerStyle);

  if (style === "tree") {
    ctx.fillStyle = "#2E8B57";
    ctx.fillRect(sx, sy, T, T);
    ctx.fillStyle = "#228B22";
    ctx.fillRect(sx+2, sy+2, T-4, T-4);
    ctx.fillStyle = "#1a6b1a";
    ctx.fillRect(sx+6, sy+6, T-12, T-12);
    ctx.fillStyle = "rgba(255,255,255,0.12)";
    ctx.fillRect(sx+3, sy+3, 8, 5);
    ctx.fillRect(sx+16, sy+8, 5, 4);

  } else if (style === "building") {
    ctx.fillStyle = "#CD853F";
    ctx.fillRect(sx, sy, T, T);

    // 좌우 프레임을 먼저 잡아서 가장자리 벽이 깨져 보이지 않게 한다.
    ctx.fillStyle = "#8B4513";
    ctx.fillRect(sx, sy, 4, T);
    ctx.fillRect(sx + T - 4, sy, 4, T);

    ctx.fillStyle = "#A0522D";
    for (let row = 0; row < 3; row++) {
      const y = sy + 2 + row * 10;
      const offset = (row % 2) * 8;
      for (let col = 0; col < 3; col++) {
        const brickX = sx + 4 + col * 8 + offset;
        if (brickX < sx + 4 || brickX + 10 > sx + T - 4) continue;
        ctx.fillRect(brickX, y, 10, 8);
      }
    }
    ctx.fillStyle = "#8B6914";
    for (let row = 0; row < 4; row++) ctx.fillRect(sx, sy + row*10, T, 2);
    ctx.fillStyle = "#8B4513";
    ctx.fillRect(sx, sy, T, 4);
    ctx.fillStyle = "#A0522D";
    ctx.fillRect(sx, sy, T, 2);

  } else if (style === "blue_shop") {
    ctx.fillStyle = "#4A7FD6";
    ctx.fillRect(sx, sy, T, T);
    ctx.fillStyle = "#274E9B";
    ctx.fillRect(sx, sy, 4, T);
    ctx.fillRect(sx + T - 4, sy, 4, T);
    ctx.fillStyle = "#6FA4FF";
    for (let row = 0; row < 3; row++) {
      const y = sy + 2 + row * 10;
      const offset = (row % 2) * 8;
      for (let col = 0; col < 3; col++) {
        const brickX = sx + 4 + col * 8 + offset;
        if (brickX < sx + 4 || brickX + 10 > sx + T - 4) continue;
        ctx.fillRect(brickX, y, 10, 8);
      }
    }
    ctx.fillStyle = "#1E3F7A";
    for (let row = 0; row < 4; row++) ctx.fillRect(sx, sy + row * 10, T, 2);
    ctx.fillRect(sx, sy, T, 4);
    ctx.fillStyle = "rgba(255,255,255,0.18)";
    ctx.fillRect(sx + 6, sy + 6, 8, 3);

  } else if (style === "pink_center") {
    ctx.fillStyle = "#F48FB1";
    ctx.fillRect(sx, sy, T, T);
    ctx.fillStyle = "#C2185B";
    ctx.fillRect(sx, sy, 4, T);
    ctx.fillRect(sx + T - 4, sy, 4, T);
    ctx.fillStyle = "#FFCDD8";
    for (let row = 0; row < 3; row++) {
      const y = sy + 2 + row * 10;
      const offset = (row % 2) * 8;
      for (let col = 0; col < 3; col++) {
        const brickX = sx + 4 + col * 8 + offset;
        if (brickX < sx + 4 || brickX + 10 > sx + T - 4) continue;
        ctx.fillRect(brickX, y, 10, 8);
      }
    }
    ctx.fillStyle = "#AD1457";
    for (let row = 0; row < 4; row++) ctx.fillRect(sx, sy + row * 10, T, 2);
    ctx.fillRect(sx, sy, T, 4);
    ctx.fillStyle = "rgba(255,255,255,0.22)";
    ctx.fillRect(sx + 6, sy + 6, 8, 3);

  } else if (style === "concrete") {
    ctx.fillStyle = "#9E9E9E";
    ctx.fillRect(sx, sy, T, T);
    ctx.fillStyle = "#BDBDBD";
    ctx.fillRect(sx+1, sy+1, T-2, T-2);
    ctx.fillStyle = "#757575";
    ctx.fillRect(sx, sy, T, 2);
    ctx.fillRect(sx, sy, 2, T);
    ctx.fillStyle = "#616161";
    ctx.fillRect(sx, sy+T-2, T, 2);
    ctx.fillRect(sx+T-2, sy, 2, T);
    ctx.fillStyle = "rgba(0,0,0,0.06)";
    ctx.fillRect(sx+4, sy+4, 6, 6);
    ctx.fillRect(sx+18, sy+14, 5, 5);

  } else if (style === "indoor") {
    ctx.fillStyle = "#8B4513";
    ctx.fillRect(sx, sy, T, T);
    ctx.fillStyle = "#A0522D";
    ctx.fillRect(sx+1, sy+1, T-2, T-2);
    ctx.fillStyle = "rgba(255,255,255,0.08)";
    ctx.fillRect(sx+2, sy+2, T-4, 4);
  }
}

function drawPath(ctx, sx, sy) {
  const T = TILE;
  ctx.fillStyle = "#C8A96E";
  ctx.fillRect(sx, sy, T, T);
  // 돌 패턴
  ctx.fillStyle = "rgba(180,140,80,0.5)";
  ctx.fillRect(sx+2,  sy+4,  12, 8);
  ctx.fillRect(sx+16, sy+2,  10, 7);
  ctx.fillRect(sx+4,  sy+18, 8,  7);
  ctx.fillRect(sx+18, sy+16, 9,  8);
  // 밝은 하이라이트
  ctx.fillStyle = "rgba(255,255,255,0.12)";
  ctx.fillRect(sx+2, sy+4, 4, 2);
  ctx.fillRect(sx+16, sy+2, 3, 2);
}

function drawWater(ctx, sx, sy) {
  const T = TILE;
  const t = Date.now() / 800;
  const wave = Math.sin(t + sx * 0.05) * 2;
  ctx.fillStyle = "#3a7bd5";
  ctx.fillRect(sx, sy, T, T);
  // 물결
  ctx.fillStyle = "rgba(100,180,255,0.35)";
  ctx.fillRect(sx, sy + 6 + wave, T, 4);
  ctx.fillRect(sx, sy + 18 + wave, T, 3);
  ctx.fillStyle = "rgba(255,255,255,0.15)";
  ctx.fillRect(sx+4, sy+8+wave, 8, 2);
  ctx.fillRect(sx+18, sy+20+wave, 6, 2);
}

function drawDeco(ctx, sx, sy) {
  const T = TILE;
  // 바닥
  ctx.fillStyle = "#DEB887";
  ctx.fillRect(sx, sy, T, T);
  // 책상/선반 느낌
  ctx.fillStyle = "#8B5E3C";
  ctx.fillRect(sx+4, sy+6, T-8, T-12);
  ctx.fillStyle = "#A0724A";
  ctx.fillRect(sx+4, sy+6, T-8, 4);
  ctx.fillStyle = "rgba(255,255,255,0.1)";
  ctx.fillRect(sx+6, sy+8, 4, T-16);
}

function drawDoor(ctx, sx, sy) {
  const T = TILE;
  // 바닥
  ctx.fillStyle = "#A0522D";
  ctx.fillRect(sx, sy, T, T);
  // 문틀
  ctx.fillStyle = "#6B3A1F";
  ctx.fillRect(sx, sy, 3, T);
  ctx.fillRect(sx+T-3, sy, 3, T);
  ctx.fillRect(sx, sy, T, 3);
  // 문 패널
  ctx.fillStyle = "#C47A3A";
  ctx.fillRect(sx+5, sy+5, T-10, T-8);
  // 손잡이
  ctx.fillStyle = "#FFD700";
  ctx.beginPath();
  ctx.arc(sx+T-9, sy+T/2, 2.5, 0, Math.PI*2);
  ctx.fill();
}

// ── 특수 캐릭터 픽셀 드로잉 ──

function drawHero(ctx, px, py) {
  const T = TILE;
  ctx.fillStyle = "rgba(0,0,0,0.2)";
  ctx.beginPath(); ctx.ellipse(px+T/2,py+T-3,9,4,0,0,Math.PI*2); ctx.fill();
  // 신발
  ctx.fillStyle = "#8B4513";
  ctx.fillRect(px+9, py+28, 6, 4); ctx.fillRect(px+17,py+28,6,4);
  // 바지
  ctx.fillStyle = "#2244AA";
  ctx.fillRect(px+9, py+21, 14, 8);
  // 셔츠 (파란 조끼)
  ctx.fillStyle = "#E63946";
  ctx.fillRect(px+8, py+13, 16, 9);
  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(px+13,py+13,6,9); // 조끼 라인
  // 팔
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(px+4, py+14,4,8); ctx.fillRect(px+24,py+14,4,8);
  // 목
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(px+13,py+11,6,4);
  // 머리
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(px+9, py+4, 14,10);
  // 눈 (방향별)
  ctx.fillStyle = "#1a1a2e";
  if (playerDir==="down")      { ctx.fillRect(px+12,py+8,3,3); ctx.fillRect(px+18,py+8,3,3); }
  else if (playerDir==="up")   { /* 뒷모습 */ }
  else if (playerDir==="right"){ ctx.fillRect(px+19,py+8,3,3); }
  else                          { ctx.fillRect(px+10,py+8,3,3); }
  // 모자 (빨간 챙 모자)
  ctx.fillStyle = "#CC0000";
  ctx.fillRect(px+8, py+2,16,5);
  ctx.fillRect(px+5, py+5,22,3);
  ctx.fillStyle = "rgba(255,255,255,0.25)";
  ctx.fillRect(px+8, py+2,16,2);
  // 모자 로고
  ctx.fillStyle = "#FFD700";
  ctx.fillRect(px+14,py+3,4,3);
}

function drawProfessor(ctx, sx, sy) {
  const T = TILE;
  ctx.fillStyle = "rgba(0,0,0,0.18)";
  ctx.beginPath(); ctx.ellipse(sx+T/2,sy+T-3,8,3,0,0,Math.PI*2); ctx.fill();
  // 신발
  ctx.fillStyle = "#333";
  ctx.fillRect(sx+9,sy+28,6,4); ctx.fillRect(sx+17,sy+28,6,4);
  // 바지
  ctx.fillStyle = "#4169E1";
  ctx.fillRect(sx+9,sy+21,14,8);
  // 흰 가운
  ctx.fillStyle = "#F0F0F0";
  ctx.fillRect(sx+7,sy+12,18,10);
  ctx.fillStyle = "#DCDCDC";
  ctx.fillRect(sx+7,sy+12,3,10); ctx.fillRect(sx+22,sy+12,3,10); // 가운 옷깃
  ctx.fillStyle = "#4169E1";
  ctx.fillRect(sx+13,sy+12,6,10); // 셔츠
  // 팔 (가운)
  ctx.fillStyle = "#F0F0F0";
  ctx.fillRect(sx+3,sy+13,5,9); ctx.fillRect(sx+24,sy+13,5,9);
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+3,sy+20,5,4); ctx.fillRect(sx+24,sy+20,5,4); // 손
  // 목
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+13,sy+10,6,4);
  // 머리
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+9,sy+3,14,10);
  // 눈 + 안경
  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(sx+11,sy+7,3,3); ctx.fillRect(sx+18,sy+7,3,3);
  ctx.strokeStyle = "#888"; ctx.lineWidth=1;
  ctx.strokeRect(sx+10,sy+6,5,5); ctx.strokeRect(sx+17,sy+6,5,5);
  ctx.beginPath(); ctx.moveTo(sx+15,sy+8); ctx.lineTo(sx+17,sy+8); ctx.stroke();
  // 흰 머리
  ctx.fillStyle = "#E8E8E8";
  ctx.fillRect(sx+9,sy+1,14,5);
  ctx.fillStyle = "#C0C0C0";
  ctx.fillRect(sx+9,sy+1,14,3);
}

function drawMom(ctx, sx, sy) {
  const T = TILE;
  ctx.fillStyle = "rgba(0,0,0,0.18)";
  ctx.beginPath(); ctx.ellipse(sx+T/2,sy+T-3,8,3,0,0,Math.PI*2); ctx.fill();
  // 신발
  ctx.fillStyle = "#C71585";
  ctx.fillRect(sx+10,sy+28,5,4); ctx.fillRect(sx+17,sy+28,5,4);
  // 치마
  ctx.fillStyle = "#FF69B4";
  ctx.fillRect(sx+8,sy+20,16,9);
  ctx.fillStyle = "#FF1493";
  ctx.fillRect(sx+8,sy+26,16,3); // 치마 밑단
  // 상의 (앞치마)
  ctx.fillStyle = "#FF69B4";
  ctx.fillRect(sx+9,sy+12,14,9);
  ctx.fillStyle = "#FFFFFF";
  ctx.fillRect(sx+12,sy+13,8,7); // 앞치마
  ctx.fillStyle = "#FFB6C1";
  ctx.fillRect(sx+13,sy+14,6,5);
  // 팔
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+4,sy+13,5,9); ctx.fillRect(sx+23,sy+13,5,9);
  // 목
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+13,sy+10,6,4);
  // 머리
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+9,sy+3,14,10);
  // 눈 + 미소
  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(sx+12,sy+7,3,3); ctx.fillRect(sx+18,sy+7,3,3);
  ctx.fillStyle = "#C71585";
  ctx.fillRect(sx+13,sy+11,6,2); // 입술
  // 갈색 단발머리
  ctx.fillStyle = "#8B4513";
  ctx.fillRect(sx+9,sy+1,14,5);
  ctx.fillRect(sx+7,sy+4,4,8);  // 왼쪽 머리
  ctx.fillRect(sx+21,sy+4,4,8); // 오른쪽 머리
  // 머리 하이라이트
  ctx.fillStyle = "#A0522D";
  ctx.fillRect(sx+11,sy+1,8,3);
}

function drawRival(ctx, sx, sy) {
  const T = TILE;
  ctx.fillStyle = "rgba(0,0,0,0.18)";
  ctx.beginPath(); ctx.ellipse(sx+T/2,sy+T-3,8,3,0,0,Math.PI*2); ctx.fill();
  // 신발 (부츠)
  ctx.fillStyle = "#00868B";
  ctx.fillRect(sx+9,sy+26,6,6); ctx.fillRect(sx+17,sy+26,6,6);
  // 바지
  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(sx+9,sy+20,14,7);
  // 상의 (청록 재킷)
  ctx.fillStyle = "#00CED1";
  ctx.fillRect(sx+8,sy+12,16,9);
  ctx.fillStyle = "#008B8B";
  ctx.fillRect(sx+8,sy+12,4,9); ctx.fillRect(sx+20,sy+12,4,9); // 재킷 옷깃
  ctx.fillStyle = "#E0FFFF";
  ctx.fillRect(sx+13,sy+13,6,7); // 안쪽 셔츠
  // 팔
  ctx.fillStyle = "#00CED1";
  ctx.fillRect(sx+3,sy+13,6,8); ctx.fillRect(sx+23,sy+13,6,8);
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+3,sy+19,6,4); ctx.fillRect(sx+23,sy+19,6,4);
  // 목
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+13,sy+10,6,4);
  // 머리
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+9,sy+3,14,10);
  // 눈 (날카로운)
  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(sx+11,sy+7,4,2); ctx.fillRect(sx+17,sy+7,4,2);
  ctx.fillRect(sx+12,sy+9,3,2); ctx.fillRect(sx+18,sy+9,3,2);
  // 청록 포니테일
  ctx.fillStyle = "#00CED1";
  ctx.fillRect(sx+9,sy+1,14,5);
  ctx.fillRect(sx+20,sy+4,5,12); // 포니테일
  ctx.fillStyle = "#008B8B";
  ctx.fillRect(sx+20,sy+4,5,3);
  // 머리띠
  ctx.fillStyle = "#FFD700";
  ctx.fillRect(sx+9,sy+5,14,2);
}

function drawBinna(ctx, sx, sy) {
  const T = TILE;
  ctx.fillStyle = "rgba(0,0,0,0.18)";
  ctx.beginPath(); ctx.ellipse(sx+T/2,sy+T-3,8,3,0,0,Math.PI*2); ctx.fill();
  ctx.fillStyle = "#324C8C";
  ctx.fillRect(sx+10,sy+27,5,5); ctx.fillRect(sx+17,sy+27,5,5);
  ctx.fillStyle = "#3A4A7A";
  ctx.fillRect(sx+9,sy+20,14,8);
  ctx.fillStyle = "#F6C2D9";
  ctx.fillRect(sx+8,sy+12,16,9);
  ctx.fillStyle = "#FFDDEB";
  ctx.fillRect(sx+12,sy+13,8,7);
  ctx.fillStyle = "#FFDEAD";
  ctx.fillRect(sx+4,sy+13,5,8); ctx.fillRect(sx+23,sy+13,5,8);
  ctx.fillRect(sx+13,sy+10,6,4);
  ctx.fillRect(sx+9,sy+4,14,10);
  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(sx+12,sy+8,3,2); ctx.fillRect(sx+18,sy+8,3,2);
  ctx.fillRect(sx+14,sy+11,4,1);
  ctx.fillStyle = "#1F2C5C";
  ctx.fillRect(sx+8,sy+1,16,5);
  ctx.fillRect(sx+7,sy+4,4,10);
  ctx.fillRect(sx+21,sy+4,4,10);
  ctx.fillRect(sx+20,sy+12,4,7);
  ctx.fillStyle = "#31457F";
  ctx.fillRect(sx+10,sy+2,10,2);
  ctx.fillStyle = "#F8E16C";
  ctx.fillRect(sx+9,sy+6,14,2);
}

function drawStarterTable(ctx, sx, sy) {
  const T = TILE;

  // 탁자 그림자
  ctx.fillStyle = "rgba(0,0,0,0.16)";
  ctx.fillRect(sx + 5, sy + 24, T - 10, 4);

  // 탁자 상판
  ctx.fillStyle = "#8B5A2B";
  ctx.fillRect(sx + 4, sy + 11, T - 8, 8);
  ctx.fillStyle = "#A66A36";
  ctx.fillRect(sx + 5, sy + 12, T - 10, 3);

  // 탁자 다리
  ctx.fillStyle = "#6E4420";
  ctx.fillRect(sx + 7, sy + 19, 3, 8);
  ctx.fillRect(sx + T - 10, sy + 19, 3, 8);

  // 몬스터볼 3개
  const balls = [7, 13, 19];
  balls.forEach(x => {
    ctx.fillStyle = "#d32f2f";
    ctx.fillRect(sx + x, sy + 8, 5, 2);
    ctx.fillStyle = "#f5f5f5";
    ctx.fillRect(sx + x, sy + 10, 5, 2);
    ctx.fillStyle = "#222";
    ctx.fillRect(sx + x, sy + 9, 5, 1);
    ctx.fillStyle = "#fff";
    ctx.fillRect(sx + x + 2, sy + 9, 1, 1);
  });
}

function drawSign(ctx, sx, sy) {
  const T = TILE;

  // 기둥
  ctx.fillStyle = "#6E4B2A";
  ctx.fillRect(sx + 14, sy + 18, 4, 12);

  // 표지판 그림자
  ctx.fillStyle = "rgba(0,0,0,0.15)";
  ctx.fillRect(sx + 6, sy + 8, 20, 10);

  // 판자
  ctx.fillStyle = "#A66A36";
  ctx.fillRect(sx + 5, sy + 6, 22, 12);
  ctx.fillStyle = "#C58A4A";
  ctx.fillRect(sx + 6, sy + 7, 20, 3);

  // 판자 결
  ctx.fillStyle = "#8B5A2B";
  ctx.fillRect(sx + 7, sy + 11, 18, 1);
  ctx.fillRect(sx + 7, sy + 14, 18, 1);
}

function drawCyndaquilDoll(ctx, sx, sy) {
  const T = TILE;
  ctx.fillStyle = "rgba(0,0,0,0.16)";
  ctx.beginPath();
  ctx.ellipse(sx + T / 2, sy + T - 4, 8, 3, 0, 0, Math.PI * 2);
  ctx.fill();

  ctx.fillStyle = "#FFDEAD";
  ctx.beginPath();
  ctx.ellipse(sx + 16, sy + 18, 8, 9, 0, 0, Math.PI * 2);
  ctx.fill();

  ctx.fillStyle = "#1a2c4a";
  ctx.fillRect(sx + 10, sy + 22, 12, 4);

  ctx.fillStyle = "#FF8C42";
  ctx.fillRect(sx + 11, sy + 8, 3, 8);
  ctx.fillRect(sx + 15, sy + 5, 3, 11);
  ctx.fillRect(sx + 19, sy + 9, 3, 7);

  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(sx + 13, sy + 15, 2, 2);
  ctx.fillRect(sx + 18, sy + 15, 2, 2);
}

function drawCaveEntrance(ctx, sx, sy) {
  const T = TILE;
  ctx.fillStyle = "#5b5b63";
  ctx.fillRect(sx + 2, sy + 8, T - 4, T - 8);
  ctx.fillStyle = "#7a7a84";
  ctx.fillRect(sx + 4, sy + 10, T - 8, 6);
  ctx.fillStyle = "#44444b";
  ctx.beginPath();
  ctx.arc(sx + T / 2, sy + 18, 10, Math.PI, 0);
  ctx.fill();
  ctx.fillStyle = "#111116";
  ctx.beginPath();
  ctx.arc(sx + T / 2, sy + 20, 8, Math.PI, 0);
  ctx.fill();
  ctx.fillStyle = "#8c8c96";
  ctx.fillRect(sx + 6, sy + 12, 4, 3);
  ctx.fillRect(sx + 22, sy + 14, 3, 3);
}

// ── NPC 드로잉 (순수 픽셀) ──
function drawNPC(ctx, npc, sx, sy) {
  const T = TILE;
  // 특수 캐릭터 분기
  if (npc.id === "professor") { drawProfessor(ctx, sx, sy); return; }
  if (npc.id === "mom" || npc.id === "mom_home") { drawMom(ctx, sx, sy); return; }
  if (npc.id === "rival_route" || npc.id === "rival_first") { drawRival(ctx, sx, sy); return; }
  if (npc.id === "binna_hometown" || npc.id === "binna_town2") { drawBinna(ctx, sx, sy); return; }
  if (npc.id === "starter_table") { drawStarterTable(ctx, sx, sy); return; }
  if (npc.id === "cyndaquil_doll") { drawCyndaquilDoll(ctx, sx, sy); return; }
  if (npc.id === "cave_route1") { drawCaveEntrance(ctx, sx, sy); return; }
  if (npc.id.startsWith("sign_")) { drawSign(ctx, sx, sy); return; }

  // 그림자
  ctx.fillStyle = "rgba(0,0,0,0.18)";
  ctx.beginPath(); ctx.ellipse(sx+T/2,sy+T-3,8,3,0,0,Math.PI*2); ctx.fill();

  const style = NPC_STYLES[npc.id] || NPC_STYLES.default;
  // 다리
  ctx.fillStyle = style.pants;
  ctx.fillRect(sx+10,sy+22,5,8); ctx.fillRect(sx+17,sy+22,5,8);
  // 몸
  ctx.fillStyle = style.shirt;
  ctx.fillRect(sx+8,sy+13,16,11);
  // 팔
  ctx.fillStyle = style.skin;
  ctx.fillRect(sx+4,sy+14,4,7); ctx.fillRect(sx+24,sy+14,4,7);
  // 머리
  ctx.fillStyle = style.skin;
  ctx.fillRect(sx+9,sy+4,14,11);
  // 눈
  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(sx+12,sy+8,3,3); ctx.fillRect(sx+18,sy+8,3,3);
  // 헤어/모자
  ctx.fillStyle = style.hair;
  ctx.fillRect(sx+9,sy+2,14,5);
  if (style.hat) { ctx.fillStyle = style.hat; ctx.fillRect(sx+7,sy+5,18,3); }
  if (style.accessory === "cane") {
    ctx.fillStyle = "#8B4513";
    ctx.fillRect(sx+26,sy+14,2,16);
  }
}

function getMapData() { return MAPS[state.currentMap]; }

const npcMoveStates = new WeakMap();

function getNpcRenderPosition(npc, ts = performance.now()) {
  const move = npcMoveStates.get(npc);
  if (!move) return { x: npc.tx * TILE, y: npc.ty * TILE };

  const progress = Math.min(1, (ts - move.startTs) / move.duration);
  const eased = easeInOutQuad(progress);
  const x = move.fromX + (move.toX - move.fromX) * eased;
  const y = move.fromY + (move.toY - move.fromY) * eased;

  if (progress >= 1) {
    npcMoveStates.delete(npc);
    return { x: move.toX, y: move.toY };
  }

  return { x, y };
}

function setNpcTile(npc, tx, ty, duration = NPC_MOVE_DURATION) {
  const current = getNpcRenderPosition(npc);
  npc.tx = tx;
  npc.ty = ty;
  npcMoveStates.set(npc, {
    fromX: current.x,
    fromY: current.y,
    toX: tx * TILE,
    toY: ty * TILE,
    startTs: performance.now(),
    duration,
  });
}

function moveNpcBy(npc, dx, dy, duration = NPC_MOVE_DURATION) {
  setNpcTile(npc, npc.tx + dx, npc.ty + dy, duration);
}

function drawMap(ts = performance.now()) {
  const map = getMapData();
  loadNpcImages(map);

  const cols = Math.ceil(CANVAS_W / TILE);
  const rows = Math.ceil(CANVAS_H / TILE);

  // 카메라: 보간된 플레이어 위치 기준
  const camX = renderX - CANVAS_W / 2 + TILE / 2;
  const camY = renderY - CANVAS_H / 2 + TILE / 2;

  // 배경
  ctx.fillStyle = map.bgColor;
  ctx.fillRect(0, 0, CANVAS_W, CANVAS_H);

  const startTX = Math.floor(camX / TILE);
  const startTY = Math.floor(camY / TILE);

  // 타일 드로잉
  for (let row = startTY - 1; row <= startTY + rows + 1; row++) {
    for (let col = startTX - 1; col <= startTX + cols + 1; col++) {
      const tileType = getTile(map, col, row);
      const sx = col * TILE - camX;
      const sy = row * TILE - camY;
      drawTile(ctx, tileType, sx, sy, map, col, row);

      // 출입구 목적지 라벨
      if (tileType === 2) {
        const door = map.doors?.find(d => d.tx === col && d.ty === row);
        if (door) {
          const destName = MAPS[door.toMap]?.name || door.toMap;
          ctx.fillStyle = "rgba(0,0,0,0.65)";
          ctx.font = "8px 'Courier New'";
          const tw = ctx.measureText(destName).width + 6;
          ctx.fillRect(sx + TILE/2 - tw/2, sy - 14, tw, 12);
          ctx.fillStyle = "#FFD700";
          ctx.textAlign = "center";
          ctx.fillText(destName, sx + TILE/2, sy - 4);
        }
      }
    }
  }

  // NPC
  const nearNpc = map.npcs?.find(n =>
    Math.abs(n.tx - state.px) + Math.abs(n.ty - state.py) === 1
  );
  map.npcs?.forEach(npc => {
    const npcPos = getNpcRenderPosition(npc, ts);
    const sx = npcPos.x - camX;
    const sy = npcPos.y - camY;
    drawNPC(ctx, npc, sx, sy);
    // 이름
    ctx.fillStyle = "rgba(0,0,0,0.55)";
    ctx.font = "9px 'Courier New'";
    const nw = ctx.measureText(npc.name).width + 6;
    ctx.fillRect(sx + TILE/2 - nw/2, sy - 14, nw, 11);
    ctx.fillStyle = "#fff";
    ctx.textAlign = "center";
    ctx.fillText(npc.name, sx + TILE/2, sy - 5);
    // [Enter] 힌트
    if (nearNpc?.id === npc.id) {
      const hint = "[Enter]";
      const hw = ctx.measureText(hint).width + 8;
      ctx.fillStyle = "rgba(0,0,0,0.75)";
      ctx.fillRect(sx + TILE/2 - hw/2, sy - 28, hw, 13);
      ctx.fillStyle = "#FFD700";
      ctx.font = "bold 9px 'Courier New'";
      ctx.fillText(hint, sx + TILE/2, sy - 17);
    }
  });

  // 플레이어 (보간 좌표 사용)
  const px = renderX - camX;
  const py = renderY - camY;
  drawHero(ctx, px, py);

  // 연구소 유도 화살표
  if (!state.events.starterChosen && state.currentMap === "hometown") {
    const labDoor = map.doors?.find(d => d.toMap === "lab");
    if (labDoor) {
      const ax = labDoor.tx * TILE - camX + TILE / 2;
      const ay = labDoor.ty * TILE - camY;
      const bounce = Math.sin(Date.now() / 400) * 5;
      ctx.save();
      ctx.fillStyle = "#FFD700";
      ctx.strokeStyle = "#000";
      ctx.lineWidth = 1;
      ctx.font = "bold 20px 'Courier New'";
      ctx.textAlign = "center";
      ctx.strokeText("▼", ax, ay - 6 + bounce);
      ctx.fillText("▼", ax, ay - 6 + bounce);
      ctx.font = "bold 9px 'Courier New'";
      ctx.fillText("연구소", ax, ay - 24 + bounce);
      ctx.restore();
    }
  }
}

function getTile(map, tx, ty) {
  if (ty < 0 || ty >= map.height || tx < 0 || tx >= map.width) return 1;
  return map.tiles[ty][tx];
}

// ============================================================
//  이동 & 충돌
// ============================================================
const keys = {};
let lastMoveTime = 0;

document.addEventListener("keydown", e => {
  keys[e.key] = true;
  if (e.key === "Enter" || e.key === " ") {
    e.preventDefault();
    if (inDialogue) {
      advanceDialogue();
    } else {
      tryInteract();
    }
  }
});
document.addEventListener("keyup", e => { keys[e.key] = false; });

function tryMove(dx, dy) {
  if (inDialogue || moveState.active) return false;
  if (dx === 1)  playerDir = "right";
  if (dx === -1) playerDir = "left";
  if (dy === 1)  playerDir = "down";
  if (dy === -1) playerDir = "up";
  const nx = state.px + dx;
  const ny = state.py + dy;
  const map = getMapData();
  const tile = getTile(map, nx, ny);
  const blockedByNpc = map.npcs?.some(n => n.tx === nx && n.ty === ny);

  if (tile === 1) return false; // 벽
  if (blockedByNpc) return false; // NPC/오브젝트 위로는 올라갈 수 없음

  moveState.active = true;
  moveState.fromX = state.px * TILE;
  moveState.fromY = state.py * TILE;
  moveState.toX = nx * TILE;
  moveState.toY = ny * TILE;
  moveState.targetX = nx;
  moveState.targetY = ny;
  moveState.startTs = 0;
  moveState.arrived = false;
  return true;
}

function finishMove() {
  if (!moveState.active || moveState.arrived) return;
  moveState.arrived = true;
  state.px = moveState.targetX;
  state.py = moveState.targetY;

  const map = getMapData();

  if (state.starter && getTile(map, state.px, state.py) === 6 && Math.random() < 0.3) {
    moveState.active = false;
    startWildBattle();
    return;
  }

  const door = map.doors?.find(d => d.tx === state.px && d.ty === state.py);
  if (door) {
    moveState.active = false;
    changeMap(door.toMap, door.toX, door.toY);
    return;
  }

  map.autoEvents?.forEach(ev => {
    if (ev.tx === state.px && ev.ty === state.py) triggerAutoEvent(ev.eventKey);
  });
}

function updateMovement(ts) {
  if (!moveState.active) return;
  if (!moveState.startTs) moveState.startTs = ts;

  const progress = Math.min(1, (ts - moveState.startTs) / MOVE_DURATION);
  const eased = easeInOutQuad(progress);
  renderX = moveState.fromX + (moveState.toX - moveState.fromX) * eased;
  renderY = moveState.fromY + (moveState.toY - moveState.fromY) * eased;

  if (progress >= 1) {
    renderX = moveState.toX;
    renderY = moveState.toY;
    finishMove();
    moveState.active = false;
  }
}

function tryInteract() {
  if (inDialogue || moveState.active) return;
  const map = getMapData();
  // 플레이어 주변 4방향 NPC 탐색
  const dirs = [[0,-1],[0,1],[-1,0],[1,0]];
  for (const [dx, dy] of dirs) {
    const npc = map.npcs?.find(n => n.tx === state.px + dx && n.ty === state.py + dy);
    if (npc) { triggerNPC(npc); return; }
  }
}

// ============================================================
//  맵 전환
// ============================================================
function changeMap(mapId, tx, ty) {
  // 이전 맵의 autoEvent 키 제거 (다른 맵 갔다 와도 재발동 안 되게)
  const prevMap = getMapData();
  prevMap.autoEvents?.forEach(ev => firedAutoEvents.delete(ev.eventKey));

  moveState.active = false;
  moveState.arrived = false;
  state.currentMap = mapId;
  state.px = tx;
  state.py = ty;
  // 맵 전환 시 보간 좌표 즉시 동기화 (순간이동 느낌 방지)
  renderX = tx * TILE;
  renderY = ty * TILE;
  saveGame();
  updateHUD();

  // 연구소 진입 시: 라이벌 먼저, 그 다음 박사
  if (mapId === "lab" && !state.events.professorMet) {
    setTimeout(() => {
      if (!state.events.rivalEventTriggered) {
        triggerRivalLabEvent(() => triggerProfessorEvent());
      } else {
        triggerProfessorEvent();
      }
    }, 300);
  }
}

// ============================================================
//  NPC 이벤트
// ============================================================
function triggerNPC(npc) {
  if (npc.id === "rival_route" || npc.id === "rival_first") {
    triggerRivalNPC(npc);
    return;
  }
  if (npc.id === "binna_hometown" || npc.id === "binna_town2") {
    triggerBinnaNPC(npc);
    return;
  }
  if (npc.id === "trainer1") {
    showDialogue(DIALOGUES.trainer1, npc.name, () => {
      showDialogue(["여행자가 배틀을 걸어왔다!"], "★ 배틀", () => {
        startTrainerBattle("trainer1", won => {
          if (won) showDialogue(["잘 싸웠어! 대단한 녀석이군."], npc.name);
          else showDialogue(["다음엔 더 강해져서 와!"], npc.name);
        });
      });
    });
    return;
  }
  if (npc.id === "nurse") {
    showDialogue(DIALOGUES.nurse, npc.name, () => {
      showDialogue(["포켓몬을 쉬게 해줄래?"], npc.name, () => {
        showChoices([
          { label: "예", callback: async () => {
            showDialogue(["그래!"], npc.name, async () => {
              await fadeScreen(true);
              healStarterFull();
              await fadeScreen(false);
            });
          }},
          { label: "아니오", callback: () => showDialogue(["다음에 또 봐~"], npc.name) },
        ]);
      });
    });
    return;
  }
  if (npc.id === "professor" && state.events.starterChosen) {
    showDialogue(DIALOGUES.professor_after_starter, npc.name);
    return;
  }
  if (npc.id === "starter_table" && state.events.professorMet && !state.events.starterChosen) {
    triggerStarterChoice();
    return;
  }
  const lines = DIALOGUES[npc.dialogueKey] || ["..."];
  showDialogue(lines, npc.name);
}

function triggerRivalNPC(npc) {
  startRivalAIConversation("안녕! 뭐하고 있어?");
}

function triggerBinnaNPC(npc) {
  if (npc.id === "binna_hometown") {
    if (!state.events.starterChosen) {
      showDialogue(DIALOGUES[npc.dialogueKey] || ["..."], npc.name);
      return;
    }
    if (!state.events.binnaCongrats) {
      triggerBinnaCongratsEvent();
      return;
    }
    startBinnaAIConversation("안녕, {playerName}! 오늘은 어디까지 가봤어?");
    return;
  }
  if (npc.id === "binna_town2" && !state.events.binnaTown2Met) {
    triggerBinnaTown2Event();
    return;
  }
  startBinnaAIConversation("안녕! 지금 뭐하고 있었어?");
}

// AI 기반 라이벌 대화: 첫 메시지를 라이벌이 말하고, 선택지 3개 제공
async function startRivalAIConversation(firstMessage) {
  if (inDialogue) return;
  showDialogue([firstMessage], "라이벌 리벨");
  dlgWaitingAI = true;
  dlgNext.style.display = "none";
  const choices = await getRivalChoices(firstMessage);
  dlgWaitingAI = false;
  if (!inDialogue) return;
  showChoices(choices.map(c => ({
    label: c,
    callback: () => continueRivalConversation(c),
  })));
}

async function continueRivalConversation(playerChoice) {
  choicesEl.style.display = "none";
  showDialogue(["..."], "라이벌 리벨");
  dlgWaitingAI = true;
  dlgNext.style.display = "none";
  const reply = await getRivalReply(playerChoice);
  if (!inDialogue) { dlgWaitingAI = false; return; }
  dlgText.textContent = reply;
  const choices = await getRivalChoices(reply);
  dlgWaitingAI = false;
  if (!inDialogue) return;
  showChoices([
    ...choices.map(c => ({
      label: c,
      callback: () => continueRivalConversation(c),
    })),
    { label: "그만 얘기할게.", callback: () => { closeDialogue(); choicesEl.style.display = "none"; } },
  ]);
}

async function startBinnaAIConversation(firstMessage) {
  if (inDialogue) return;
  showDialogue([firstMessage], "빛나");
  dlgWaitingAI = true;
  dlgNext.style.display = "none";
  const choices = await getRivalChoices(firstMessage);
  dlgWaitingAI = false;
  if (!inDialogue) return;
  showChoices(choices.map(c => ({
    label: c,
    callback: () => continueBinnaConversation(c),
  })));
}

async function continueBinnaConversation(playerChoice) {
  choicesEl.style.display = "none";
  showDialogue(["..."], "빛나");
  dlgWaitingAI = true;
  dlgNext.style.display = "none";
  const reply = await getBinnaReply(playerChoice);
  if (!inDialogue) { dlgWaitingAI = false; return; }
  dlgText.textContent = reply;
  const choices = await getRivalChoices(reply);
  dlgWaitingAI = false;
  if (!inDialogue) return;
  showChoices([
    ...choices.map(c => ({
      label: c,
      callback: () => continueBinnaConversation(c),
    })),
    { label: "다음에 얘기하자.", callback: () => { closeDialogue(); choicesEl.style.display = "none"; } },
  ]);
}

// ============================================================
//  박사 이벤트
// ============================================================
function triggerRivalLabEvent(callback) {
  if (state.events.rivalEventTriggered) { if (callback) callback(); return; }
  state.events.rivalEventTriggered = true;
  saveGame();
  showDialogue(DIALOGUES.rival_lab_entrance, "라이벌 리벨", () => {
    // 퇴장 애니메이션
    const map = getMapData();
    const rival = map.npcs?.find(n => n.id === "rival_lab");
    if (rival) {
      let step = 0;
      const exit = setInterval(() => {
        moveNpcBy(rival, 0, 1);
        step++;
        if (step >= 4) {
          clearInterval(exit);
          map.npcs = map.npcs.filter(n => n.id !== "rival_lab");
          if (callback) callback();
        }
      }, NPC_STEP_DELAY);
    } else {
      if (callback) callback();
    }
  });
}

function triggerProfessorEvent() {
  state.events.professorMet = true;
  showDialogue(DIALOGUES.professor, "오크 박사", () => {
    // 스타터 선택 유도
    showChoices([
      { label: "몬스터 볼 살펴보기", callback: triggerStarterChoice },
      { label: "나중에 할게요",      callback: closeDialogue },
    ]);
  });
}

function triggerBinnaCongratsEvent() {
  if (state.events.binnaCongrats) return;
  const map = MAPS.hometown;
  const binna = map.npcs?.find(n => n.id === "binna_hometown");
  state.phase = "cutscene";

  if (!binna) {
    state.events.binnaCongrats = true;
    saveGame();
    showDialogue(["와, {playerName}! 포켓몬 받았구나!", "정말 축하해! 이제 같이 멋진 모험 하자!"], "빛나", () => {
      state.phase = "game";
    });
    return;
  }

  const homePos = { tx: 13, ty: 13 };
  setNpcTile(binna, 10, 8, 1);
  const path = [[-1, 0], [0, -1]];
  let step = 0;
  const walk = setInterval(() => {
    const move = path[step++];
    if (move) {
      moveNpcBy(binna, move[0], move[1]);
      return;
    }
    clearInterval(walk);
    state.events.binnaCongrats = true;
    saveGame();
    showDialogue(["와, {playerName}! 포켓몬 받았구나!", "정말 축하해! 이제 같이 멋진 모험 하자!"], "빛나", () => {
      setNpcTile(binna, homePos.tx, homePos.ty, 1);
      state.phase = "game";
    });
  }, NPC_STEP_DELAY);
}

function triggerBinnaTown2Event() {
  if (state.events.binnaTown2Met) return;
  state.events.binnaTown2Met = true;
  saveGame();
  showDialogue(["또 만났네, {playerName}!", "여기까지 오다니 정말 빠르다. 같이 둘러보고 싶었어!"], "빛나", () => {
    state.phase = "game";
  });
}

function triggerStarterChoice() {
  closeDialogue();
  openStarterOverlay(starter => {
    state.starter = createStarterFromTemplate(starter);
    state.events.starterChosen = true;
    saveGame();
    updateHUD();

    const reaction = DIALOGUES.rivalStarterReaction[starter.id] || [];
    showDialogue(DIALOGUES.professor_after, "오크 박사", () => {
      changeMap("hometown", 8, 7);
      setTimeout(() => {
        changeMap("hometown", 8, 7);
        setTimeout(triggerBinnaCongratsEvent, 250);
      }, 400);
    });
  });
}

// ============================================================
//  라이벌 첫 만남 (연구소 밖)
// ============================================================
function triggerRivalFirstMeet(starterReaction = []) {
  if (state.events.rivalMet) return;
  state.events.rivalMet = true;
  saveGame();
  const firstMsg = starterReaction[0] || "늦었잖아! 나는 벌써 박사님한테 받았다고.";
  startRivalAIConversation(firstMsg);
}

// ============================================================
//  자동 이벤트
// ============================================================
const firedAutoEvents = new Set();

function triggerAutoEvent(eventKey) {
  if (eventKey === "town2_arrival" && state.events.town2Visited) return;
  if (firedAutoEvents.has(eventKey)) return;
  firedAutoEvents.add(eventKey);

  // rival_lab_entrance는 changeMap에서 직접 처리
  if (eventKey === "rival_lab_entrance") return;

  if (eventKey === "town2_arrival" && !state.events.town2Visited) {
    state.events.town2Visited = true;
    saveGame();
    const lines = DIALOGUES.autoEvents[eventKey] || ["..."];
    showDialogue(lines, "★ 알림", () => {
      if (!state.events.binnaTown2Met) triggerBinnaTown2Event();
    });
    return;
  }

  const lines = DIALOGUES.autoEvents[eventKey] || ["..."];
  showDialogue(lines, "★ 알림");
}

// ============================================================
//  저장 / 불러오기
// ============================================================
const API_BASE_KEY = "pokemonDemoApiBase";
const DEFAULT_API_BASE = "http://localhost:5140";
let apiBase = localStorage.getItem(API_BASE_KEY) || DEFAULT_API_BASE;
let activeSaveId = localStorage.getItem("pokemonDemoActiveSaveId") || null;
let activeSlotNumber = Number(localStorage.getItem("pokemonDemoActiveSlot") || "1");
let saveStartedAt = Date.now();
let saveWriteTimer = null;

serverUrlInput.value = apiBase;

function resetState(playerName, mode = "single", roomId = null) {
  state.playerName = playerName || "주인공";
  state.starter = null;
  state.currentMap = "hometown";
  state.px = 9;
  state.py = 9;
  state.events = { ...DEFAULT_EVENTS };
  state.mode = mode;
  state.roomId = roomId;
  firedAutoEvents.clear();
}

function localSaveKey() {
  return `pokemonDemo:${state.mode || "single"}:${activeSlotNumber}`;
}

function buildSavePayload() {
  const starter = normalizeStarter(state.starter);
  return {
    slotNumber: activeSlotNumber,
    mode: state.mode || "single",
    playerName: state.playerName,
    currentMap: state.currentMap,
    positionX: state.px,
    positionY: state.py,
    starter: starter ? {
      id: starter.id,
      name: starter.name,
      level: starter.level,
      currentHp: starter.currentHp,
    } : null,
    events: state.events,
    gameState: {
      playerName: state.playerName,
      starter,
      currentMap: state.currentMap,
      px: state.px,
      py: state.py,
      events: state.events,
      mode: state.mode,
      roomId: state.roomId,
    },
    playTimeSeconds: Math.floor((Date.now() - saveStartedAt) / 1000),
  };
}

function saveGame() {
  const payload = buildSavePayload();
  localStorage.setItem(localSaveKey(), JSON.stringify(payload.gameState));
  localStorage.setItem("pokemonDemo", JSON.stringify(payload.gameState));

  if (state.phase !== "game") return;
  if (saveWriteTimer) clearTimeout(saveWriteTimer);
  saveWriteTimer = setTimeout(() => {
    syncSaveToServer(payload).catch(() => {});
  }, 450);
}

function loadGame() {
  const raw = localStorage.getItem(localSaveKey()) || localStorage.getItem("pokemonDemo");
  if (!raw) return false;
  try {
    const saved = JSON.parse(raw);
    applySavedState(saved);
    return true;
  } catch { return false; }
}

function applySavedState(saved) {
  Object.assign(state, saved);
  state.events = { ...DEFAULT_EVENTS, ...(saved.events || {}) };
  state.starter = normalizeStarter(state.starter);
  state.mode = saved.mode || state.mode || "single";
  state.roomId = saved.roomId || null;
  firedAutoEvents.clear();
}

async function syncSaveToServer(payload) {
  const url = activeSaveId ? `${apiBase}/api/saves/${activeSaveId}` : `${apiBase}/api/saves`;
  const res = await fetch(url, {
    method: activeSaveId ? "PUT" : "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!res.ok) throw new Error(`save failed ${res.status}`);
  const saved = await res.json();
  activeSaveId = saved.id;
  localStorage.setItem("pokemonDemoActiveSaveId", activeSaveId);
}

async function fetchJson(url, options) {
  const res = await fetch(url, options);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

// ============================================================
//  게임 루프
// ============================================================
function gameLoop(ts) {
  const canRenderMap = state.phase === "game" || state.phase === "cutscene";
  if (!canRenderMap) { requestAnimationFrame(gameLoop); return; }

  if (state.phase === "game") {
    updateMovement(ts);

    // 이동 처리
    if (!moveState.active && ts - lastMoveTime > MOVE_DELAY) {
      if      (keys["ArrowUp"]    || keys["w"]) { if (tryMove(0, -1)) lastMoveTime = ts; }
      else if (keys["ArrowDown"]  || keys["s"]) { if (tryMove(0,  1)) lastMoveTime = ts; }
      else if (keys["ArrowLeft"]  || keys["a"]) { if (tryMove(-1, 0)) lastMoveTime = ts; }
      else if (keys["ArrowRight"] || keys["d"]) { if (tryMove( 1, 0)) lastMoveTime = ts; }
    }
  }

  drawMap(ts);
  requestAnimationFrame(gameLoop);
}

// ============================================================
//  시작 화면
// ============================================================
function setMenuPanel(panel) {
  [saveMenuPanel, multiMenuPanel, settingsPanel].forEach(el => {
    el.style.display = el === panel ? "block" : "none";
  });
}

function setStatus(message) {
  menuStatus.textContent = message || "";
}

function getEnteredPlayerName() {
  const nameVal = document.getElementById("name-input").value.trim();
  return nameVal || "주인공";
}

function beginGame({ mode = "single", slotNumber = 1, saveId = null, savedState = null, roomId = null } = {}) {
  activeSlotNumber = slotNumber;
  activeSaveId = saveId;
  localStorage.setItem("pokemonDemoActiveSlot", String(activeSlotNumber));
  if (activeSaveId) localStorage.setItem("pokemonDemoActiveSaveId", activeSaveId);
  else localStorage.removeItem("pokemonDemoActiveSaveId");

  if (savedState) {
    applySavedState(savedState);
    state.mode = mode || state.mode || "single";
    state.roomId = roomId || state.roomId || null;
  } else {
    resetState(getEnteredPlayerName(), mode, roomId);
  }

  startScreen.style.display = "none";
  gameWrapper.style.display = "flex";

  state.phase = "game";
  saveStartedAt = Date.now();
  renderX = state.px * TILE;
  renderY = state.py * TILE;
  updateHUD();
  saveGame();
  requestAnimationFrame(gameLoop);

  setTimeout(() => {
    const hometown = MAPS.hometown;
    const mom = hometown?.npcs?.find(n => n.id === "mom");
    if (mom && state.currentMap === "hometown" && !state.events.starterChosen) {
      showDialogue(DIALOGUES.mom, mom.name);
    }
  }, 200);
}

async function renderSaveSlots() {
  setMenuPanel(saveMenuPanel);
  setStatus("세이브 슬롯을 불러오는 중...");
  saveSlotsEl.innerHTML = "";

  try {
    const saves = await fetchJson(`${apiBase}/api/saves?mode=single`);
    const bySlot = new Map(saves.map(save => [save.slotNumber, save]));
    for (let slot = 1; slot <= 3; slot++) {
      const save = bySlot.get(slot);
      const button = document.createElement("button");
      button.className = "slot-card";
      button.type = "button";
      button.innerHTML = save ? `
        <div class="slot-title">슬롯 ${slot} · ${save.playerName}</div>
        <div class="slot-meta">${save.currentMap} (${save.positionX}, ${save.positionY}) · ${save.starter?.name || "파트너 없음"} Lv.${save.starter?.level || "-"}</div>
        <div class="slot-meta">마지막 저장: ${new Date(save.updatedAt).toLocaleString()}</div>
      ` : `
        <div class="slot-title">슬롯 ${slot}</div>
        <div class="slot-meta">빈 슬롯</div>
      `;
      button.onclick = async () => {
        activeSlotNumber = slot;
        if (!save) {
          beginGame({ mode: "single", slotNumber: slot });
          return;
        }
        const detail = await fetchJson(`${apiBase}/api/saves/${save.id}`);
        beginGame({ mode: "single", slotNumber: slot, saveId: detail.id, savedState: detail.gameState });
      };
      saveSlotsEl.appendChild(button);
    }
    setStatus("");
  } catch {
    setStatus("서버 세이브를 불러오지 못했습니다. 로컬 저장으로 이어합니다.");
    const button = document.createElement("button");
    button.className = "slot-card";
    button.type = "button";
    button.innerHTML = `<div class="slot-title">로컬 저장</div><div class="slot-meta">브라우저에 저장된 진행도를 불러옵니다.</div>`;
    button.onclick = () => {
      loadGame();
      beginGame({ mode: state.mode || "single", slotNumber: activeSlotNumber, savedState: { ...state } });
    };
    saveSlotsEl.appendChild(button);
  }
}

async function renderRooms() {
  setMenuPanel(multiMenuPanel);
  setStatus("방 목록을 불러오는 중...");
  roomListEl.innerHTML = "";
  try {
    const rooms = await fetchJson(`${apiBase}/api/rooms`);
    rooms.forEach(room => {
      const button = document.createElement("button");
      button.className = "room-card";
      button.type = "button";
      button.innerHTML = `
        <div class="room-title">${room.roomName}</div>
        <div class="room-meta">${room.mapName} · ${room.playerCount}/${room.maxPlayers}명</div>
      `;
      button.onclick = async () => {
        const joined = await fetchJson(`${apiBase}/api/rooms/${room.roomId}/join`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ playerName: getEnteredPlayerName() }),
        });
        setStatus(`멀티 방 참가: ${joined.room.roomName}`);
        beginGame({ mode: "multi", slotNumber: 1, roomId: joined.room.roomId });
      };
      roomListEl.appendChild(button);
    });
    if (rooms.length === 0) {
      roomListEl.innerHTML = `<div class="room-card"><div class="room-meta">생성된 방이 없습니다.</div></div>`;
    }
    setStatus("");
  } catch {
    setStatus("서버 방 목록을 불러오지 못했습니다. 백엔드 서버와 PostgreSQL 설정을 확인하세요.");
  }
}

document.getElementById("single-btn").addEventListener("click", async () => {
  setStatus("싱글 세션을 준비하는 중...");
  try {
    await fetchJson(`${apiBase}/api/sessions/single`, { method: "POST" });
  } catch {}
  beginGame({ mode: "single", slotNumber: 1 });
});

document.getElementById("saves-btn").addEventListener("click", renderSaveSlots);
document.getElementById("multiplayer-btn").addEventListener("click", renderRooms);
document.getElementById("settings-btn").addEventListener("click", () => {
  setMenuPanel(settingsPanel);
  setStatus("");
});
document.getElementById("save-back-btn").addEventListener("click", () => setMenuPanel(null));
document.getElementById("multi-back-btn").addEventListener("click", () => setMenuPanel(null));
document.getElementById("settings-back-btn").addEventListener("click", () => setMenuPanel(null));
document.getElementById("save-settings-btn").addEventListener("click", () => {
  apiBase = serverUrlInput.value.trim() || DEFAULT_API_BASE;
  localStorage.setItem(API_BASE_KEY, apiBase);
  setStatus("서버 주소를 저장했습니다.");
});
document.getElementById("create-room-btn").addEventListener("click", async () => {
  const roomName = document.getElementById("room-name-input").value.trim() || "모험 방";
  try {
    await fetchJson(`${apiBase}/api/rooms`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ roomName, mapId: "hometown" }),
    });
    await renderRooms();
  } catch {
    setStatus("방 생성에 실패했습니다.");
  }
});

// 채팅 전송
// 채팅 UI 제거됨 - 라이벌 대화는 선택지 방식으로 통합
