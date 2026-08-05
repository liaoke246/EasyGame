import Phaser from "phaser";
import {
  ENVIRONMENT_ATLAS_KEY,
  GAME_ATLAS_KEY,
  TERRAIN_DIRT_KEY,
  TERRAIN_GRASS_KEY,
  TERRAIN_WILD_KEY,
  explosionFrame,
  preloadGameAtlas,
  registerGameAtlasFrames,
} from "./game-atlas";
import type { NetworkClient } from "./network";
import { PlayerView } from "./player-view";
import { RocketView } from "./rocket-view";
import { ZombieView } from "./zombie-view";
import type {
  AttackEvent,
  Direction,
  InputPayload,
  NotificationEvent,
  Obstacle,
  PublicPlayer,
  WeaponId,
  WelcomePayload,
  WorldSnapshot,
} from "./types";

type MovementInput = Pick<
  InputPayload,
  "up" | "down" | "left" | "right"
>;

interface WorldSceneCallbacks {
  onLoadProgress?: (progress: number, fileKey?: string) => void;
  onLoadError?: (fileKey: string) => void;
  onReady?: () => void;
}

export class WorldScene extends Phaser.Scene {
  private readonly entities = new Map<string, PlayerView>();
  private readonly zombieEntities = new Map<string, ZombieView>();
  private readonly rocketEntities = new Map<string, RocketView>();
  private readonly unsubscribeCallbacks: Array<() => void> = [];
  private keyboard?: {
    cursors: Phaser.Types.Input.Keyboard.CursorKeys;
    w: Phaser.Input.Keyboard.Key;
    a: Phaser.Input.Keyboard.Key;
    s: Phaser.Input.Keyboard.Key;
    d: Phaser.Input.Keyboard.Key;
    attack: Phaser.Input.Keyboard.Key;
    weapon1: Phaser.Input.Keyboard.Key;
    weapon2: Phaser.Input.Keyboard.Key;
    weapon3: Phaser.Input.Keyboard.Key;
  };
  private virtualInput: MovementInput = {
    up: false,
    down: false,
    left: false,
    right: false,
  };
  private previousInput = "";
  private lastInputSentAt = 0;
  private cameraFollowing = false;
  private selectedWeapon: WeaponId = "smg";
  private virtualFiring = false;
  private latencyMs = 0;

  constructor(
    private readonly network: NetworkClient,
    private readonly welcome: WelcomePayload,
    private readonly callbacks: WorldSceneCallbacks = {},
  ) {
    super("world");
  }

  preload(): void {
    this.load.on(Phaser.Loader.Events.PROGRESS, (progress: number) => {
      this.callbacks.onLoadProgress?.(progress);
    });
    this.load.on(
      Phaser.Loader.Events.FILE_PROGRESS,
      (file: Phaser.Loader.File) => {
        this.callbacks.onLoadProgress?.(this.load.progress, file.key);
      },
    );
    this.load.on(
      Phaser.Loader.Events.FILE_LOAD_ERROR,
      (file: Phaser.Loader.File) => {
        this.callbacks.onLoadError?.(file.key);
      },
    );
    preloadGameAtlas(this);
  }

  create(): void {
    registerGameAtlasFrames(this);
    this.cameras.main.setBounds(
      0,
      0,
      this.welcome.world.width,
      this.welcome.world.height,
    );
    this.cameras.main.setZoom(1);
    this.cameras.main.setRoundPixels(false);
    this.drawWorld();
    this.configureKeyboard();
    this.configureTouchControls();

    this.unsubscribeCallbacks.push(
      this.network.onSnapshot((snapshot) => this.applySnapshot(snapshot)),
      this.network.onAttack((event) => this.showAttack(event)),
      this.network.onNotification((event) => this.showNotification(event)),
      this.network.onNetworkStats((stats) => {
        this.latencyMs = stats.latencyMs ?? 0;
        setText(
          "#latency-value",
          stats.latencyMs === null ? "--" : String(stats.latencyMs),
        );
        setText(
          "#packet-loss-value",
          stats.samples === 0 ? "--" : String(stats.packetLossPercent),
        );
        const dot = document.querySelector<HTMLElement>("#connection-dot");
        if (dot) {
          dot.dataset.state = stats.connected ? "connected" : "offline";
        }
      }),
    );

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      for (const unsubscribe of this.unsubscribeCallbacks) {
        unsubscribe();
      }
    });

    this.callbacks.onLoadProgress?.(1);
    this.time.delayedCall(80, () => this.callbacks.onReady?.());
  }

  update(time: number, delta: number): void {
    const deltaSeconds = Math.min(delta / 1_000, 0.05);
    const input = this.currentInput();
    const localEntity = this.entities.get(this.welcome.playerId);
    localEntity?.predictMovement(input, deltaSeconds, this.welcome.world);

    for (const entity of this.entities.values()) {
      entity.update(deltaSeconds, time);
    }
    for (const zombie of this.zombieEntities.values()) {
      zombie.update(deltaSeconds, time);
    }
    for (const rocket of this.rocketEntities.values()) {
      rocket.update(deltaSeconds, time);
    }

    if (localEntity && !this.cameraFollowing) {
      this.cameras.main.centerOn(
        localEntity.container.x,
        localEntity.container.y,
      );
      this.cameras.main.startFollow(
        localEntity.container,
        true,
        0.16,
        0.16,
      );
      this.cameraFollowing = true;
    }

    this.sendCurrentInput(time, input);
  }

  private drawWorld(): void {
    const { width, height, obstacles } = this.welcome.world;
    const centerX = width / 2;
    const centerY = height / 2;
    this.add
      .tileSprite(centerX, centerY, width, height, TERRAIN_GRASS_KEY)
      .setDepth(-20);
    this.add
      .tileSprite(width * 0.17, height * 0.19, width * 0.34, height * 0.38, TERRAIN_WILD_KEY)
      .setAlpha(0.42)
      .setDepth(-19);
    this.add
      .tileSprite(width * 0.83, height * 0.78, width * 0.34, height * 0.44, TERRAIN_WILD_KEY)
      .setAlpha(0.35)
      .setDepth(-19);

    this.add
      .tileSprite(centerX, centerY, width, 154, TERRAIN_DIRT_KEY)
      .setDepth(-15);
    this.add
      .tileSprite(centerX, centerY, 154, height, TERRAIN_DIRT_KEY)
      .setDepth(-14);
    this.add
      .rectangle(centerX, centerY, width, 4, 0xd5aa63, 0.18)
      .setDepth(-13);
    this.add
      .rectangle(centerX, centerY, 4, height, 0xd5aa63, 0.18)
      .setDepth(-13);

    this.drawGroundDetails(width, height);

    for (const obstacle of obstacles) {
      this.drawObstacle(obstacle);
    }

    this.add
      .text(centerX + 118, centerY - 118, "苔 原 谷", {
        fontFamily: '"KaiTi", "STKaiti", serif',
        fontSize: "23px",
        color: "#5f5234",
        stroke: "#e3cf9b",
        strokeThickness: 4,
      })
      .setDepth(440);
    this.add
      .text(centerX + 119, centerY - 92, "MOSSFIELD", {
        fontFamily: "Georgia, serif",
        fontSize: "9px",
        letterSpacing: 5,
        color: "#74633d",
      })
      .setDepth(440);

    const border = this.add.graphics();
    border.lineStyle(14, 0x263b2c, 1);
    border.strokeRect(5, 5, width - 10, height - 10);
    border.setDepth(height + 200);
  }

  private drawGroundDetails(width: number, height: number): void {
    for (let index = 0; index < 26; index += 1) {
      const x = 90 + ((index * 347) % (width - 180));
      const y = 90 + ((index * 229) % (height - 180));
      if (Math.abs(x - width / 2) < 115 || Math.abs(y - height / 2) < 115) {
        continue;
      }
      this.add
        .image(x, y, ENVIRONMENT_ATLAS_KEY, "prop-flowers")
        .setDisplaySize(62 + (index % 3) * 8, 48 + (index % 2) * 6)
        .setAlpha(0.72)
        .setDepth(Math.round(y - 8));
    }

    const decorations: Array<[number, number, string, number, number]> = [
      [width * 0.33, height * 0.22, "prop-stump", 92, 86],
      [width * 0.72, height * 0.28, "prop-crates", 112, 90],
      [width * 0.38, height * 0.76, "prop-fence", 150, 78],
      [width * 0.57, height * 0.18, "prop-lantern", 66, 102],
      [width * 0.44, height * 0.84, "prop-lantern", 58, 92],
    ];
    for (const [x, y, frame, displayWidth, displayHeight] of decorations) {
      this.add
        .image(x, y, ENVIRONMENT_ATLAS_KEY, frame)
        .setDisplaySize(displayWidth, displayHeight)
        .setDepth(Math.round(y));
    }
  }

  private drawObstacle(obstacle: Obstacle): void {
    const graphics = this.add.graphics();
    graphics.setDepth(obstacle.y + obstacle.height);

    switch (obstacle.type) {
      case "cabin":
        this.drawCabin(graphics, obstacle);
        break;
      case "pond":
        this.drawPond(graphics, obstacle);
        break;
      case "tree":
        this.drawTree(graphics, obstacle);
        break;
      case "rock":
        this.drawRock(graphics, obstacle);
        break;
      case "garden":
        this.drawGarden(graphics, obstacle);
        break;
    }
  }

  private drawCabin(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.destroy();
    this.add
      .image(
        x + width / 2,
        y + height + 28,
        ENVIRONMENT_ATLAS_KEY,
        obstacle.id.endsWith("2") ? "prop-cabin-side" : "prop-cabin",
      )
      .setOrigin(0.5, 1)
      .setDisplaySize(width + 90, height + 120)
      .setDepth(y + height);
  }

  private drawPond(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.destroy();
    this.add
      .image(x + width / 2, y + height / 2, ENVIRONMENT_ATLAS_KEY, "prop-pond")
      .setDisplaySize(width + 70, height + 70)
      .setDepth(y + height - 8);
  }

  private drawTree(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.destroy();
    const pine = hash2d(Math.round(x), Math.round(y)) % 3 === 0;
    this.add
      .image(
        x + width / 2,
        y + height + 20,
        ENVIRONMENT_ATLAS_KEY,
        pine ? "prop-pine" : "prop-oak",
      )
      .setOrigin(0.5, 1)
      .setDisplaySize(pine ? 132 : 150, pine ? 178 : 148)
      .setDepth(y + height);
  }

  private drawRock(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.destroy();
    this.add
      .image(x + width / 2, y + height + 7, ENVIRONMENT_ATLAS_KEY, "prop-rock")
      .setOrigin(0.5, 1)
      .setDisplaySize(width + 58, height + 52)
      .setDepth(y + height);
  }

  private drawGarden(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.destroy();
    this.add
      .image(x + width / 2, y + height / 2, ENVIRONMENT_ATLAS_KEY, "prop-garden")
      .setDisplaySize(width + 55, height + 55)
      .setDepth(y + height);
  }

  private configureKeyboard(): void {
    const keyboard = this.input.keyboard;
    if (!keyboard) {
      return;
    }

    this.keyboard = {
      cursors: keyboard.createCursorKeys(),
      w: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.W),
      a: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.A),
      s: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.S),
      d: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.D),
      attack: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.SPACE),
      weapon1: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.ONE),
      weapon2: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.TWO),
      weapon3: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.THREE),
    };

    this.keyboard.weapon1.on("down", () => this.selectWeapon("smg"));
    this.keyboard.weapon2.on("down", () => this.selectWeapon("shotgun"));
    this.keyboard.weapon3.on("down", () => this.selectWeapon("rocket"));
  }

  private configureTouchControls(): void {
    const movementButtons = document.querySelectorAll<HTMLButtonElement>(
      "[data-move]",
    );
    for (const button of movementButtons) {
      const direction = button.dataset.move as keyof MovementInput;
      const setPressed = (pressed: boolean): void => {
        this.virtualInput[direction] = pressed;
        button.classList.toggle("is-pressed", pressed);
      };
      button.addEventListener("pointerdown", (event) => {
        event.preventDefault();
        button.setPointerCapture(event.pointerId);
        setPressed(true);
      });
      button.addEventListener("pointerup", () => setPressed(false));
      button.addEventListener("pointercancel", () => setPressed(false));
      button.addEventListener("lostpointercapture", () => setPressed(false));
    }

    const attackButton =
      document.querySelector<HTMLButtonElement>("#touch-attack");
    attackButton?.addEventListener("pointerdown", (event) => {
        event.preventDefault();
        attackButton.setPointerCapture(event.pointerId);
        this.virtualFiring = true;
      });
    attackButton?.addEventListener("pointerup", () => {
      this.virtualFiring = false;
    });
    attackButton?.addEventListener("pointercancel", () => {
      this.virtualFiring = false;
    });
    attackButton?.addEventListener("lostpointercapture", () => {
      this.virtualFiring = false;
    });

    for (const button of document.querySelectorAll<HTMLButtonElement>(
      "[data-weapon]",
    )) {
      button.addEventListener("pointerdown", (event) => {
        event.preventDefault();
        this.selectWeapon(button.dataset.weapon as WeaponId);
      });
    }
    this.selectWeapon(this.selectedWeapon);
  }

  private currentInput(): InputPayload {
    const keyboard = this.keyboard;
    return {
      up:
        this.virtualInput.up ||
        keyboard?.w.isDown === true ||
        keyboard?.cursors.up.isDown === true,
      down:
        this.virtualInput.down ||
        keyboard?.s.isDown === true ||
        keyboard?.cursors.down.isDown === true,
      left:
        this.virtualInput.left ||
        keyboard?.a.isDown === true ||
        keyboard?.cursors.left.isDown === true,
      right:
        this.virtualInput.right ||
        keyboard?.d.isDown === true ||
        keyboard?.cursors.right.isDown === true,
      fire: this.virtualFiring || keyboard?.attack.isDown === true,
      weapon: this.selectedWeapon,
    };
  }

  private selectWeapon(weapon: WeaponId): void {
    if (weapon !== "smg" && weapon !== "shotgun" && weapon !== "rocket") {
      return;
    }
    this.selectedWeapon = weapon;
    const labels: Record<WeaponId, string> = {
      smg: "冲锋枪",
      shotgun: "喷子",
      rocket: "火箭筒",
    };
    setText("#current-weapon", labels[weapon]);
    setText("#touch-weapon-name", labels[weapon]);
    for (const button of document.querySelectorAll<HTMLButtonElement>(
      "[data-weapon]",
    )) {
      button.classList.toggle("is-selected", button.dataset.weapon === weapon);
    }
  }

  private sendCurrentInput(time: number, input: InputPayload): void {
    const serialized = JSON.stringify(input);
    if (serialized !== this.previousInput || time - this.lastInputSentAt > 500) {
      this.previousInput = serialized;
      this.lastInputSentAt = time;
      this.network.sendInput(input);
    }
  }

  private applySnapshot(snapshot: WorldSnapshot): void {
    const presentPlayers = new Set<string>();
    const presentZombies = new Set<string>();
    const presentRockets = new Set<string>();

    for (const player of snapshot.players) {
      presentPlayers.add(player.id);
      let entity = this.entities.get(player.id);
      if (!entity) {
        entity = new PlayerView(
          this,
          player,
          player.id === this.welcome.playerId,
        );
        this.entities.set(player.id, entity);
      }
      entity.applyState(
        player,
        player.id === this.welcome.playerId ? this.latencyMs : 0,
      );

      if (player.id === this.welcome.playerId) {
        this.updateHud(player, snapshot.players.length);
      }
    }

    for (const [id, entity] of this.entities) {
      if (!presentPlayers.has(id)) {
        entity.destroy();
        this.entities.delete(id);
      }
    }

    for (const zombie of snapshot.zombies) {
      presentZombies.add(zombie.id);
      let entity = this.zombieEntities.get(zombie.id);
      if (!entity) {
        entity = new ZombieView(this, zombie);
        this.zombieEntities.set(zombie.id, entity);
      }
      entity.applyState(zombie);
    }

    for (const [id, entity] of this.zombieEntities) {
      if (!presentZombies.has(id)) {
        entity.destroy();
        this.zombieEntities.delete(id);
      }
    }

    for (const rocket of snapshot.rockets) {
      presentRockets.add(rocket.id);
      let entity = this.rocketEntities.get(rocket.id);
      if (!entity) {
        entity = new RocketView(this, rocket);
        this.rocketEntities.set(rocket.id, entity);
      }
      entity.applyState(rocket);
    }

    for (const [id, entity] of this.rocketEntities) {
      if (!presentRockets.has(id)) {
        entity.destroy();
        this.rocketEntities.delete(id);
      }
    }

    const onlineCount = document.querySelector("#online-count");
    if (onlineCount) {
      onlineCount.textContent = String(snapshot.players.length);
    }
    setText("#zombie-count", String(snapshot.zombies.length));
  }

  private updateHud(player: PublicPlayer, online: number): void {
    const healthRatio = Math.round((player.health / player.maxHealth) * 100);
    setText("#health-value", `${player.health} / ${player.maxHealth}`);
    setText("#kill-count", String(player.kills));
    setText("#online-count", String(online));
    this.selectWeapon(player.weapon);
    const healthFill = document.querySelector<HTMLElement>("#hud-health-fill");
    if (healthFill) {
      healthFill.style.width = `${healthRatio}%`;
      healthFill.dataset.level =
        healthRatio > 55 ? "healthy" : healthRatio > 25 ? "hurt" : "danger";
    }
    document
      .querySelector("#respawn-message")
      ?.classList.toggle("is-visible", player.respawning);
  }

  private showAttack(event: AttackEvent): void {
    const killed = new Set(event.killedZombieIds);
    for (const zombieId of event.hitZombieIds) {
      this.zombieEntities.get(zombieId)?.showHit(killed.has(zombieId));
    }

    if (event.weapon === "rocket" && event.phase === "impact") {
      this.showExplosion(event.x, event.y - 14);
      return;
    }

    const vector = directionVector(event.direction);
    this.entities
      .get(event.attackerId)
      ?.showRecoil(event.weapon === "rocket" ? -5 : event.weapon === "shotgun" ? -3.5 : -1.5);
    this.showMuzzleFlash(
      event.x + vector.x * 25,
      event.y + vector.y * 25 - 18,
      event.weapon,
      vector,
    );

    if (event.weapon === "rocket") {
      return;
    }

    const color = event.weapon === "shotgun" ? 0xffc773 : 0xffefae;
    const width = event.weapon === "shotgun" ? 1 : 2;
    for (const trace of event.traces) {
      this.showTracer(event.x, event.y - 18, trace.endX, trace.endY, color, width);
      if (trace.hit) {
        this.showImpactSpark(trace.endX, trace.endY - 16, color);
      }
    }
  }

  private showMuzzleFlash(
    x: number,
    y: number,
    weapon: WeaponId,
    vector: { x: number; y: number },
  ): void {
    const colors =
      weapon === "rocket"
        ? [0xfff2aa, 0xff9c4a, 0xe84f32]
        : [0xfff7c7, 0xffcc68, 0xf27b3d];
    const angle = Math.atan2(vector.y, vector.x);
    const flashCount = weapon === "shotgun" ? 9 : weapon === "rocket" ? 8 : 5;
    for (let index = 0; index < flashCount; index += 1) {
      const spread = (Math.random() - 0.5) * (weapon === "shotgun" ? 0.8 : 0.42);
      const distance = Phaser.Math.Between(4, weapon === "rocket" ? 22 : 15);
      const particle = this.add
        .rectangle(
          x + Math.cos(angle + spread) * distance,
          y + Math.sin(angle + spread) * distance,
          Phaser.Math.Between(5, weapon === "rocket" ? 15 : 10),
          Phaser.Math.Between(2, 4),
          colors[index % colors.length],
          0.95,
        )
        .setRotation(angle + spread)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(Math.round(y + 240));
      this.tweens.add({
        targets: particle,
        x: particle.x + Math.cos(angle + spread) * Phaser.Math.Between(8, 22),
        y: particle.y + Math.sin(angle + spread) * Phaser.Math.Between(8, 22),
        alpha: 0,
        scaleX: 0.2,
        scaleY: 0.35,
        duration: 75 + index * 13,
        ease: "Quad.easeOut",
        onComplete: () => particle.destroy(),
      });
    }

    for (let index = 0; index < 3; index += 1) {
      const smoke = this.add
        .circle(
          x - vector.x * index * 3,
          y - vector.y * index * 3,
          3 + index,
          0x9aa58c,
          0.22,
        )
        .setDepth(Math.round(y + 225));
      this.tweens.add({
        targets: smoke,
        x: smoke.x - vector.x * Phaser.Math.Between(5, 13) + Phaser.Math.Between(-4, 4),
        y: smoke.y - vector.y * Phaser.Math.Between(5, 13) - Phaser.Math.Between(3, 9),
        scale: 2.2,
        alpha: 0,
        duration: 260 + index * 70,
        ease: "Sine.easeOut",
        onComplete: () => smoke.destroy(),
      });
    }

    if (weapon !== "rocket") {
      const sideX = -vector.y;
      const sideY = vector.x;
      const casing = this.add
        .rectangle(x - vector.x * 9, y - vector.y * 9, 5, 2, 0xd6a94f, 1)
        .setRotation(angle + Math.PI / 2)
        .setDepth(Math.round(y + 232));
      this.tweens.add({
        targets: casing,
        x: casing.x + sideX * Phaser.Math.Between(14, 24),
        y: casing.y + sideY * Phaser.Math.Between(10, 18) + 10,
        angle: Phaser.Math.Between(140, 320),
        alpha: 0,
        duration: 340,
        ease: "Quad.easeOut",
        onComplete: () => casing.destroy(),
      });
    }
  }

  private showTracer(
    startX: number,
    startY: number,
    endX: number,
    endY: number,
    color: number,
    width: number,
  ): void {
    const glow = this.add.graphics().setDepth(Math.round(startY + 220));
    glow.lineStyle(width + 7, color, 0.08);
    glow.beginPath();
    glow.moveTo(startX, startY);
    glow.lineTo(endX, endY - 16);
    glow.strokePath();
    glow.lineStyle(width + 2, color, 0.35);
    glow.beginPath();
    glow.moveTo(startX, startY);
    glow.lineTo(endX, endY - 16);
    glow.strokePath();
    glow.lineStyle(width, 0xfffae8, 0.95);
    glow.beginPath();
    glow.moveTo(startX, startY);
    glow.lineTo(endX, endY - 16);
    glow.strokePath();
    glow.setBlendMode(Phaser.BlendModes.ADD);
    const angle = Math.atan2(endY - 16 - startY, endX - startX);
    const bullet = this.add
      .rectangle(startX, startY, width === 1 ? 7 : 12, 2, 0xfffae8, 1)
      .setRotation(angle)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(Math.round(startY + 235));
    const distance = Phaser.Math.Distance.Between(startX, startY, endX, endY);
    this.tweens.add({
      targets: bullet,
      x: endX,
      y: endY - 16,
      alpha: 0.25,
      duration: Phaser.Math.Clamp(distance * 0.16, 38, 95),
      ease: "Linear",
      onComplete: () => bullet.destroy(),
    });
    this.tweens.add({
      targets: glow,
      alpha: 0,
      duration: width === 1 ? 115 : 145,
      ease: "Quad.easeOut",
      onComplete: () => glow.destroy(),
    });
  }

  private showImpactSpark(x: number, y: number, color: number): void {
    const ring = this.add
      .circle(x, y, 5, 0x000000, 0)
      .setStrokeStyle(2, color, 0.7)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(Math.round(y + 248));
    this.tweens.add({
      targets: ring,
      scale: 2.8,
      alpha: 0,
      duration: 150,
      onComplete: () => ring.destroy(),
    });
    for (let index = 0; index < 9; index += 1) {
      const angle = (Math.PI * 2 * index) / 9 + Math.random() * 0.4;
      const spark = this.add
        .rectangle(x, y, Phaser.Math.Between(3, 7), 2, index % 3 === 0 ? 0xfff4bd : color, 0.95)
        .setRotation(angle)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(Math.round(y + 250));
      this.tweens.add({
        targets: spark,
        x: x + Math.cos(angle) * Phaser.Math.Between(14, 34),
        y: y + Math.sin(angle) * Phaser.Math.Between(14, 34),
        alpha: 0,
        duration: Phaser.Math.Between(140, 260),
        ease: "Quad.easeOut",
        onComplete: () => spark.destroy(),
      });
    }
  }

  private showExplosion(x: number, y: number): void {
    let frameIndex = 0;
    const blast = this.add
      .image(x, y, GAME_ATLAS_KEY, explosionFrame(frameIndex))
      .setDisplaySize(150, 150)
      .setDepth(Math.round(y + 301));
    this.time.addEvent({
      delay: 62,
      repeat: 5,
      callback: () => {
        frameIndex += 1;
        blast.setFrame(explosionFrame(frameIndex));
        blast.setDisplaySize(150 + frameIndex * 5, 150 + frameIndex * 5);
        if (frameIndex === 5) {
          this.tweens.add({
            targets: blast,
            alpha: 0,
            duration: 170,
            onComplete: () => blast.destroy(),
          });
        }
      },
    });
    const ring = this.add
      .circle(x, y, 24, 0x000000, 0)
      .setStrokeStyle(5, 0xff8a3d, 0.9)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(Math.round(y + 299));
    this.tweens.add({
      targets: ring,
      scale: 4.6,
      alpha: 0,
      duration: 360,
      ease: "Quad.easeOut",
      onComplete: () => {
        ring.destroy();
      },
    });
    for (let index = 0; index < 22; index += 1) {
      const angle = (Math.PI * 2 * index) / 22 + Math.random() * 0.22;
      const distance = Phaser.Math.Between(45, 120);
      const color = [0xffdb72, 0xff8d42, 0xcc4934, 0x493e36][index % 4];
      const debris = this.add
        .rectangle(x, y, Phaser.Math.Between(3, 7), Phaser.Math.Between(3, 7), color, 0.95)
        .setDepth(Math.round(y + 310));
      this.tweens.add({
        targets: debris,
        x: x + Math.cos(angle) * distance,
        y: y + Math.sin(angle) * distance,
        angle: Phaser.Math.Between(-180, 180),
        alpha: 0,
        scale: 0.25,
        duration: Phaser.Math.Between(320, 580),
        ease: "Quad.easeOut",
        onComplete: () => debris.destroy(),
      });
    }
    this.cameras.main.shake(180, 0.006);
  }

  private showNotification(event: NotificationEvent): void {
    if (event.kind === "hit") {
      return;
    }
    const feed = document.querySelector("#event-feed");
    if (!feed) {
      return;
    }

    const item = document.createElement("div");
    item.className = `feed-item feed-item--${event.kind}`;
    item.textContent = event.text;
    feed.prepend(item);

    while (feed.children.length > 4) {
      feed.lastElementChild?.remove();
    }
    window.setTimeout(() => item.classList.add("is-leaving"), 3_400);
    window.setTimeout(() => item.remove(), 3_800);
  }
}

function directionVector(direction: Direction): { x: number; y: number } {
  switch (direction) {
    case "up":
      return { x: 0, y: -1 };
    case "down":
      return { x: 0, y: 1 };
    case "left":
      return { x: -1, y: 0 };
    case "right":
      return { x: 1, y: 0 };
  }
}

function hash2d(x: number, y: number): number {
  return Math.abs((x * 73856093) ^ (y * 19349663));
}

function setText(selector: string, value: string): void {
  const element = document.querySelector(selector);
  if (element) {
    element.textContent = value;
  }
}
