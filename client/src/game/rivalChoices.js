export const RIVAL_CHOICE_POOLS = {
  question: ["왜 그렇게 말해?", "어디로 갈 건데?", "박사님이 말했어?", "같이 가볼래?", "정말 괜찮아?", "뭘 본 거야?"],
  taunt: ["알겠어.", "같이 가볼래?", "나도 열심히 할게.", "먼저 보고 올게.", "정말 괜찮아?", "조심해서 갈게."],
  warning: ["조심할게.", "넌 안 무서워?", "같이 가자.", "그래도 갈래.", "뭐가 있는 거야?", "알겠어."],
  starter: ["내 파트너야.", "잘 어울리지?", "금방 강해질걸.", "네 몬스터는 어때?", "박사님이 주셨어.", "멋지지?"],
  greeting: ["나도 방금 왔어.", "뭐하고 있었어?", "같이 얘기할래?", "먼저 보고 있었어?", "무슨 일 있어?", "반가워."],
  generic: ["그렇구나.", "나도 그렇게 생각해.", "같이 가보자.", "왜 그렇게 말해?", "알겠어.", "조심해서 갈게."],
};

export function classifyRivalMessage(message) {
  const text = (message || "").replace(/\s+/g, " ").trim();
  if (!text) return "generic";
  if (/(파이리|꼬부기|이상해씨|파트너|스타터|불 타입|물 타입|풀 타입|박사님한테 받)/.test(text)) return "starter";
  if (/(조심|위험|겁|무서|괜히|주의)/.test(text)) return "warning";
  if (/(먼저 강해|늦었|내가 더 강|안 통할 거|두고 봐|이길 거)/.test(text)) return "taunt";
  if (/[?？]|어디|왜|뭐|정말|같이 .*갈래|궁금/.test(text)) return "question";
  if (/(안녕|왔네|왔구나|반갑|기다리고)/.test(text)) return "greeting";
  return "generic";
}

export function normalizeChoiceText(choice) {
  return String(choice || "")
    .replace(/["'`]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

export function isValidRivalChoice(choice, type) {
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

export function buildFallbackChoices(rivalMessage) {
  const type = classifyRivalMessage(rivalMessage);
  const pool = RIVAL_CHOICE_POOLS[type] || RIVAL_CHOICE_POOLS.generic;
  return pool.slice(0, 3);
}

export function sanitizeRivalChoices(rawChoices, rivalMessage) {
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

