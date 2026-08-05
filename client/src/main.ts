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
  enterButton.textContent = "正在进入危机区…";
  loginStatus.textContent = "连接游戏服务器";
  loadingPanel.classList.remove("is-hidden");
  loadingPanel.dataset.phase = "connecting";
  setLoadingProgress(0.04, "连接生存服务器", "正在建立实时连接…");

  try {
    const welcome = await network.connect();
    loadingPanel.dataset.phase = "loading";
    setLoadingProgress(0.1, "准备战斗系统", "正在组装方块模型…");
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
      backgroundColor: "#cdbb96",
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
            progress >= 1 ? "正在部署竞技场" : "构建游戏世界",
            describeAsset(fileKey),
          );
        },
        onLoadError: (fileKey) => {
          loadingFile.textContent = `资源 ${fileKey} 加载失败，正在重试或使用缓存…`;
        },
        onReady: () => {
          loadingPanel.dataset.phase = "ready";
          setLoadingProgress(1, "准备完成", "进入 BLOCK CRISIS");
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
    return "正在构建竞技场与动作系统…";
  }
  const labels: Record<string, string> = {
    "block-models": "组装方块角色、武器与竞技场…",
  };
  return labels[fileKey] ?? "构建游戏世界…";
}
