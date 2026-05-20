import { createBattleRuntime } from "../battle/createBattleRuntime.js";
import { loadGameData } from "../data/gameData.js";
import { createGameRuntime } from "../game/createGameRuntime.js";

export async function bootstrapClient() {
  const { maps, dialogues } = await loadGameData();
  const game = createGameRuntime({
    maps,
    dialogues,
    env: window.POKEMON2_ENV || {},
  });
  const battle = createBattleRuntime(game);
  game.setBattleRuntime(battle);

  return { game, battle };
}
