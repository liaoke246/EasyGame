import Phaser from "phaser";
import "./style.css";
import { NetworkClient } from "./network";
import { WorldScene } from "./world-scene";

const network = new NetworkClient();
const enterButton = requireElement<HTMLButtonElement>("#enter-button");
const loginStatus = requireElement<HTMLElement>("#login-status");
const loginScreen = requireElement<HTMLElement>("#login-screen");
const gameShell = requireElement<HTMLElement>("#game-shell");
const loadingPanel = requireElement<HTMLElement>("#loading-panel");
const loadingLabel = requireElement<HTMLElement>("#loading-label");
const loadingFile = requireElement<HTMLElement>("#loading-file");
const loadingProgressValue = requireElement<HTMLElement>(
  "#loading-progress-value",
);
const loadingProgressFill = requireElement<HTMLElement>(
  "#loading-progress-fill",
);

let game: Phaser.Game | undefined;

enterButton.addEventListener("click", async () => {
  enterButton.disabled = true;
  enterButton.textContent = "正在寻找山谷…";
  loginStatus.textContent = "连接游戏服务器";
  loadingPanel.classList.remove("is-hidden");
  loadingPanel.dataset.phase = "connecting";
  setLoadingProgress(0.04, "连接山谷服务器", "正在建立实时连接…");

  try {
    const welcome = await network.connect();
    loadingPanel.dataset.phase = "loading";
    setLoadingProgress(0.1, "准备游戏资源", "正在清点地图与角色贴图…");
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
      roundPixels: false,
      antialias: false,
      fps: {
        target: 60,
        smoothStep: true,
      },
      render: {
        antialias: false,
        roundPixels: false,
        powerPreference: "high-performance",
      },
      scale: {
        mode: Phaser.Scale.RESIZE,
        autoCenter: Phaser.Scale.CENTER_BOTH,
      },
      scene: new WorldScene(network, welcome, {
        onLoadProgress: (progress, fileKey) => {
          const overallProgress = 0.1 + progress * 0.9;
          setLoadingProgress(
            overallProgress,
            progress >= 1 ? "正在进入山谷" : "载入游戏资源",
            describeAsset(fileKey),
          );
        },
        onLoadError: (fileKey) => {
          loadingFile.textContent = `资源 ${fileKey} 加载失败，正在重试或使用缓存…`;
        },
        onReady: () => {
          loadingPanel.dataset.phase = "ready";
          setLoadingProgress(1, "准备完成", "欢迎来到苔原谷");
          window.setTimeout(() => {
            loginScreen.classList.add("is-leaving");
            window.setTimeout(
              () => loginScreen.classList.add("is-hidden"),
              420,
            );
          }, 140);
        },
      }),
    });
  } catch (error) {
    loginStatus.textContent =
      error instanceof Error ? `连接失败：${error.message}` : "连接失败";
    enterButton.disabled = false;
    enterButton.textContent = "重新进入";
    loadingPanel.classList.add("is-hidden");
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

function setLoadingProgress(
  progress: number,
  label: string,
  detail: string,
): void {
  const normalized = Math.max(0, Math.min(1, progress));
  const percent = Math.round(normalized * 100);
  loadingLabel.textContent = label;
  loadingFile.textContent = detail;
  loadingProgressValue.textContent = `${percent}%`;
  loadingProgressFill.style.width = `${percent}%`;
  loadingPanel.setAttribute("aria-valuenow", String(percent));
}

function describeAsset(fileKey?: string): string {
  if (!fileKey) {
    return "正在整理地图与动画…";
  }
  const labels: Record<string, string> = {
    "easygame-atlas-v2": "载入武器与战斗特效…",
    "easygame-environment-v2": "载入山谷建筑与景物…",
    "easygame-hero-body-armless-v1": "载入角色步伐…",
    "easygame-weapon-overlay-v2": "装配武器与持枪动作…",
    "easygame-explosion-v3": "载入火箭爆炸特效…",
    "easygame-zombie-walker-walk-v2": "载入行者步伐…",
    "easygame-zombie-runner-walk-v2": "载入疾行者步伐…",
    "easygame-zombie-brute-walk-v2": "载入巨尸步伐…",
    "terrain-grass-v2": "铺设草地…",
    "terrain-dirt-v2": "铺设道路…",
    "terrain-wild-v2": "生长荒草…",
    "terrain-soil-v2": "整理农田…",
  };
  return labels[fileKey] ?? "载入游戏资源…";
}
