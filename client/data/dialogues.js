const DIALOGUES = loadGameData("dialogues.json");

function loadGameData(fileName) {
  const request = new XMLHttpRequest();
  request.open("GET", `data/${fileName}`, false);
  request.send(null);

  if ((request.status >= 200 && request.status < 300) || request.status === 0) {
    return JSON.parse(request.responseText);
  }

  throw new Error(`Failed to load ${fileName}: HTTP ${request.status}`);
}
