export async function loadGameData() {
  const [maps, dialogues] = await Promise.all([
    fetchJson("data/maps.json"),
    fetchJson("data/dialogues.json"),
  ]);

  return { maps, dialogues };
}

async function fetchJson(path) {
  const response = await fetch(path);
  if (!response.ok) throw new Error(`Failed to load ${path}: HTTP ${response.status}`);
  return response.json();
}
