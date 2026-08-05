import Phaser from "phaser";
import {
  GAME_ATLAS_KEY,
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

  constructor(
    private readonly network: NetworkClient,
    private readonly welcome: WelcomePayload,
  ) {
    super("world");
  }

  preload(): void {
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
    this.cameras.main.setZoom(1.15);
    this.drawWorld();
    this.configureKeyboard();
    this.configureTouchControls();

    this.unsubscribeCallbacks.push(
      this.network.onSnapshot((snapshot) => this.applySnapshot(snapshot)),
      this.network.onAttack((event) => this.showAttack(event)),
      this.network.onNotification((event) => this.showNotification(event)),
      this.network.onNetworkStats((stats) => {
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
      this.cameras.main.startFollow(
        localEntity.container,
        true,
        0.08,
        0.08,
      );
      this.cameraFollowing = true;
    }

    this.sendCurrentInput(time, input);
  }

  private drawWorld(): void {
    const { width, height, obstacles } = this.welcome.world;
    this.add.rectangle(width / 2, height / 2, width, height, 0x83a95f);

    const ground = this.add.graphics();
    for (let y = 0; y < height; y += 32) {
      for (let x = 0; x < width; x += 32) {
        const variant = hash2d(x, y) % 4;
        ground.fillStyle(
          [0x86ad62, 0x80a65c, 0x88af65, 0x7fa25c][variant],
          1,
        );
        ground.fillRect(x, y, 32, 32);
      }
    }

    ground.fillStyle(0xc5ac76, 1);
    ground.fillRect(0, 478, width, 126);
    ground.fillRect(874, 0, 128, height);
    ground.fillStyle(0xd0b985, 0.38);
    ground.fillRect(0, 492, width, 9);
    ground.fillRect(890, 0, 9, height);

    this.drawGroundDetails(ground, width, height);

    for (const obstacle of obstacles) {
      this.drawObstacle(obstacle);
    }

    this.add
      .text(1_005, 452, "苔 原 谷", {
        fontFamily: '"KaiTi", "STKaiti", serif',
        fontSize: "23px",
        color: "#5f5234",
        stroke: "#e3cf9b",
        strokeThickness: 4,
      })
      .setDepth(440);
    this.add
      .text(1_006, 477, "MOSSFIELD", {
        fontFamily: "Georgia, serif",
        fontSize: "9px",
        letterSpacing: 5,
        color: "#74633d",
      })
      .setDepth(440);

    const border = this.add.graphics();
    border.lineStyle(10, 0x486b43, 1);
    border.strokeRect(5, 5, width - 10, height - 10);
    border.setDepth(height + 200);
  }

  private drawGroundDetails(
    graphics: Phaser.GameObjects.Graphics,
    width: number,
    height: number,
  ): void {
    const flowerColors = [0xffe58f, 0xf3a6a0, 0xd9c1f0, 0xf6f0de];
    for (let index = 0; index < 145; index += 1) {
      const x = 24 + ((index * 137) % (width - 48));
      const y = 24 + ((index * 83) % (height - 48));
      if (y > 450 && y < 630) {
        continue;
      }
      graphics.fillStyle(flowerColors[index % flowerColors.length], 0.8);
      graphics.fillRect(x, y, 3, 3);
      graphics.fillStyle(0x547c49, 0.8);
      graphics.fillRect(x + 1, y + 3, 1, 4);
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
    graphics.fillStyle(0x6b4634, 0.2);
    graphics.fillEllipse(x + width / 2, y + height + 12, width * 0.92, 38);
    graphics.fillStyle(0xb97b51, 1);
    graphics.fillRect(x + 25, y + 78, width - 50, height - 78);
    graphics.fillStyle(0x8f5941, 1);
    for (let line = y + 94; line < y + height; line += 20) {
      graphics.fillRect(x + 25, line, width - 50, 4);
    }
    graphics.fillStyle(0x65443d, 1);
    graphics.fillTriangle(x, y + 92, x + width / 2, y, x + width, y + 92);
    graphics.fillStyle(0x83584b, 1);
    graphics.fillTriangle(
      x + 22,
      y + 86,
      x + width / 2,
      y + 15,
      x + width - 22,
      y + 86,
    );
    graphics.fillStyle(0x573a2e, 1);
    graphics.fillRect(x + width / 2 - 25, y + height - 72, 50, 72);
    graphics.fillStyle(0xd9b860, 1);
    graphics.fillRect(x + width / 2 + 13, y + height - 38, 5, 5);
    graphics.fillStyle(0x9ccbd2, 1);
    graphics.fillRect(x + 58, y + 116, 46, 40);
    graphics.fillRect(x + width - 104, y + 116, 46, 40);
    graphics.lineStyle(5, 0xe6d2a9, 1);
    graphics.strokeRect(x + 58, y + 116, 46, 40);
    graphics.strokeRect(x + width - 104, y + 116, 46, 40);
  }

  private drawPond(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.fillStyle(0x5c7c4f, 0.55);
    graphics.fillRoundedRect(x - 8, y - 8, width + 16, height + 16, 70);
    graphics.fillStyle(0x70b9b1, 1);
    graphics.fillRoundedRect(x, y, width, height, 64);
    graphics.fillStyle(0x8bcac1, 0.75);
    graphics.fillRoundedRect(x + 18, y + 22, width - 36, height - 46, 52);
    graphics.lineStyle(3, 0xc5ece1, 0.55);
    graphics.beginPath();
    graphics.moveTo(x + 70, y + 70);
    graphics.lineTo(x + 160, y + 70);
    graphics.moveTo(x + 240, y + 148);
    graphics.lineTo(x + 335, y + 148);
    graphics.strokePath();
    graphics.fillStyle(0x5c9250, 1);
    graphics.fillCircle(x + 105, y + 142, 14);
    graphics.fillCircle(x + 278, y + 72, 12);
    graphics.fillStyle(0xf5d47b, 1);
    graphics.fillCircle(x + 105, y + 142, 4);
  }

  private drawTree(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.fillStyle(0x344c34, 0.2);
    graphics.fillEllipse(x + width / 2, y + height + 4, width, 20);
    graphics.fillStyle(0x72503a, 1);
    graphics.fillRect(x + width / 2 - 9, y + height - 39, 18, 39);
    graphics.fillStyle(0x386b45, 1);
    graphics.fillCircle(x + 24, y + 34, 27);
    graphics.fillCircle(x + width - 24, y + 35, 28);
    graphics.fillCircle(x + width / 2, y + 20, 34);
    graphics.fillStyle(0x4f8550, 1);
    graphics.fillCircle(x + 27, y + 25, 18);
    graphics.fillCircle(x + width - 27, y + 23, 17);
    graphics.fillCircle(x + width / 2, y + 9, 19);
  }

  private drawRock(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.fillStyle(0x52635e, 0.2);
    graphics.fillEllipse(x + width / 2, y + height + 4, width, 13);
    graphics.fillStyle(0x77837c, 1);
    graphics.fillRoundedRect(x, y + 5, width, height - 5, 12);
    graphics.fillStyle(0xa6ada5, 1);
    graphics.fillTriangle(x + 8, y + 12, x + width / 2, y, x + width - 5, y + 13);
  }

  private drawGarden(
    graphics: Phaser.GameObjects.Graphics,
    obstacle: Obstacle,
  ): void {
    const { x, y, width, height } = obstacle;
    graphics.fillStyle(0x9a704e, 1);
    graphics.fillRoundedRect(x, y, width, height, 8);
    for (let row = y + 24; row < y + height - 12; row += 38) {
      graphics.fillStyle(0x76513a, 1);
      graphics.fillRect(x + 18, row, width - 36, 20);
      for (let plant = x + 35; plant < x + width - 25; plant += 40) {
        graphics.fillStyle(0x4b7c45, 1);
        graphics.fillCircle(plant, row + 9, 7);
        graphics.fillStyle(0x78a44f, 1);
        graphics.fillCircle(plant + 5, row + 7, 5);
      }
    }
    graphics.lineStyle(6, 0xd7b16c, 1);
    graphics.strokeRect(x, y, width, height);
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
      entity.applyState(player);

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
    this.showMuzzleFlash(
      event.x + vector.x * 25,
      event.y + vector.y * 25 - 18,
      event.weapon,
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

  private showMuzzleFlash(x: number, y: number, weapon: WeaponId): void {
    const colors =
      weapon === "rocket"
        ? [0xfff2aa, 0xff9c4a, 0xe84f32]
        : [0xfff7c7, 0xffcc68, 0xf27b3d];
    for (let index = 0; index < 5; index += 1) {
      const particle = this.add
        .circle(
          x + Phaser.Math.Between(-4, 4),
          y + Phaser.Math.Between(-4, 4),
          Phaser.Math.Between(2, weapon === "rocket" ? 6 : 4),
          colors[index % colors.length],
          0.95,
        )
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(Math.round(y + 240));
      this.tweens.add({
        targets: particle,
        alpha: 0,
        scale: 0.2,
        duration: 90 + index * 18,
        ease: "Quad.easeOut",
        onComplete: () => particle.destroy(),
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
    glow.lineStyle(width + 4, color, 0.13);
    glow.beginPath();
    glow.moveTo(startX, startY);
    glow.lineTo(endX, endY - 16);
    glow.strokePath();
    glow.lineStyle(width, color, 0.92);
    glow.beginPath();
    glow.moveTo(startX, startY);
    glow.lineTo(endX, endY - 16);
    glow.strokePath();
    glow.setBlendMode(Phaser.BlendModes.ADD);
    this.tweens.add({
      targets: glow,
      alpha: 0,
      duration: 105,
      ease: "Quad.easeOut",
      onComplete: () => glow.destroy(),
    });
  }

  private showImpactSpark(x: number, y: number, color: number): void {
    for (let index = 0; index < 5; index += 1) {
      const angle = (Math.PI * 2 * index) / 5 + Math.random() * 0.4;
      const spark = this.add
        .rectangle(x, y, 3, 2, color, 0.95)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(Math.round(y + 250));
      this.tweens.add({
        targets: spark,
        x: x + Math.cos(angle) * Phaser.Math.Between(10, 25),
        y: y + Math.sin(angle) * Phaser.Math.Between(10, 25),
        alpha: 0,
        duration: Phaser.Math.Between(120, 220),
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
