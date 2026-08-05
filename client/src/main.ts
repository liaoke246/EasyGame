import Phaser from "phaser";
import "./style.css";
import { NetworkClient } from "./network";
import { WorldScene } from "./world-scene";

const network = new NetworkClient();
const enterButton = requireElement<HTMLButtonElement>("#enter-button");
const loginStatus = requireElement<HTMLElement>("#login-status");
const loginScreen = requireElement<HTMLElement>("#login-screen");
const gameShell = requireElement<HTMLElement>("#game-shell");

let game: Phaser.Game | undefined;

enterButton.addEventListener("click", async () => {
  enterButton.disabled = true;
  enterButton.textContent = "正在寻找山谷…";
  loginStatus.textContent = "连接游戏服务器";

  try {
    const welcome = await network.connect();
    setText("#player-id", welcome.identity.displayId);
    setText("#player-role", welcome.identity.roleName);
    const portrait = requireElement<HTMLElement>("#player-portrait");
    portrait.dataset.character = welcome.identity.characterId;
    portrait.style.setProperty("--character-color", welcome.identity.color);

    gameShell.classList.remove("is-hidden");
    game = new Phaser.Game({
      type: Phaser.AUTO,
      parent: "game",
      width: window.innerWidth,
      height: window.innerHeight,
      backgroundColor: "#26392f",
      pixelArt: true,
      roundPixels: true,
      antialias: false,
      scale: {
        mode: Phaser.Scale.RESIZE,
        autoCenter: Phaser.Scale.CENTER_BOTH,
      },
      scene: new WorldScene(network, welcome),
    });

    loginScreen.classList.add("is-leaving");
    window.setTimeout(() => loginScreen.classList.add("is-hidden"), 420);
  } catch (error) {
    loginStatus.textContent =
      error instanceof Error ? `连接失败：${error.message}` : "连接失败";
    enterButton.disabled = false;
    enterButton.textContent = "重新进入";
  }
});

window.addEventListener("beforeunload", () => {
  game?.destroy(true);
  network.disconnect();
});

function requireElement<T extends Element>(selector: string): T {
  const element = document.querySelector<T>(selector);
  if (!element) {
    throw new Error(`Missing element: ${selector}`);
  }
  return element;
}

function setText(selector: string, value: string): void {
  const element = document.querySelector(selector);
  if (element) {
    element.textContent = value;
  }
}
