import Phaser from "phaser";
import {
  WEAPON_COOLDOWN_MS,
  directionVector,
  weaponMuzzleOffset,
} from "@easygame/shared";
import type { NetworkClient } from "./network";
import { PlayerView } from "./player-view";
import { RocketView } from "./rocket-view";
import { ZombieView } from "./zombie-view";
import type {
  AttackEvent,
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
  private lastPredictedAttackAt = Number.NEGATIVE_INFINITY;

  constructor(
    private readonly network: NetworkClient,
    private readonly welcome: WelcomePayload,
    private readonly callbacks: WorldSceneCallbacks = {},
  ) {
    super("world");
  }

  preload(): void {
    this.callbacks.onLoadProgress?.(0.72, "block-models");
  }

  create(): void {
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
    localEntity?.setTriggerHeld(input.fire === true);
    this.showPredictedLocalAttack(time, input, localEntity);

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
      .rectangle(centerX, centerY, width, height, 0xcdbb96)
      .setDepth(-20);
    const grid = this.add.graphics().setDepth(-19);
    grid.lineStyle(1, 0x8c8068, 0.18);
    for (let x = 0; x <= width; x += 64) {
      grid.lineBetween(x, 0, x, height);
    }
    for (let y = 0; y <= height; y += 64) {
      grid.lineBetween(0, y, width, y);
    }
    this.add.rectangle(centerX, centerY, width - 120, 150, 0xdacaa9, 0.7).setDepth(-18);
    this.add.rectangle(centerX, centerY, 150, height - 120, 0xdacaa9, 0.7).setDepth(-18);
    this.add.rectangle(centerX, centerY, width - 120, 3, 0x91866f, 0.24).setDepth(-17);
    this.add.rectangle(centerX, centerY, 3, height - 120, 0x91866f, 0.24).setDepth(-17);

    this.drawGroundDetails(width, height);

    for (const obstacle of obstacles) {
      this.drawObstacle(obstacle);
    }

    this.add
      .text(centerX + 105, centerY - 105, "BLOCK CRISIS", {
        fontFamily: '"Arial Black", sans-serif',
        fontSize: "24px",
        color: "#7a6e59",
        stroke: "#e6d7b7",
        strokeThickness: 3,
      })
      .setDepth(440);
    this.add
      .text(centerX + 107, centerY - 76, "SURVIVAL ARENA // 03", {
        fontFamily: "monospace",
        fontSize: "10px",
        letterSpacing: 2,
        color: "#8e8067",
      })
      .setDepth(440);

    const border = this.add.graphics();
    border.lineStyle(14, 0x313438, 1);
    border.strokeRect(5, 5, width - 10, height - 10);
    border.setDepth(height + 200);
  }

  private drawGroundDetails(width: number, height: number): void {
    for (let index = 0; index < 68; index += 1) {
      const x = 44 + ((index * 347) % (width - 88));
      const y = 44 + ((index * 229) % (height - 88));
      const dark = index % 5 === 0;
      this.add
        .rectangle(
          x,
          y,
          3 + (index % 4) * 2,
          dark ? 3 : 2,
          dark ? 0x6e6658 : 0xede0c1,
          dark ? 0.28 : 0.2,
        )
        .setRotation((index % 7) * 0.31)
        .setDepth(-16);
    }
    for (let index = 0; index < 12; index += 1) {
      const x = 180 + ((index * 503) % (width - 360));
      const y = 160 + ((index * 317) % (height - 320));
      this.add
        .ellipse(x, y, 32 + (index % 3) * 13, 12 + (index % 2) * 6, 0x8b3d34, 0.11)
        .setRotation((index % 5) * 0.47)
        .setDepth(-15);
    }
  }

  private drawObstacle(obstacle: Obstacle): void {
    switch (obstacle.type) {
      case "cabin":
        this.drawArenaBlock(obstacle, 0xd8d8d2, 0xa8aaa9);
        break;
      case "pond":
        this.drawArenaPit(obstacle);
        break;
      case "tree":
        this.drawArenaCrate(obstacle, 0x8e3431);
        break;
      case "rock":
        this.drawArenaBlock(obstacle, 0x9ca09f, 0x6f7374);
        break;
      case "garden":
        this.drawArenaCrate(obstacle, 0x6c6f70);
        break;
    }
  }

  private drawArenaBlock(
    obstacle: Obstacle,
    topColor: number,
    sideColor: number,
  ): void {
    const { x, y, width, height } = obstacle;
    const graphics = this.add.graphics().setDepth(y + height);
    graphics.fillStyle(0x26282a, 0.22);
    graphics.fillRect(x + 10, y + 13, width, height);
    graphics.fillStyle(sideColor, 1);
    graphics.fillRect(x, y + 9, width, height);
    graphics.fillStyle(topColor, 1);
    graphics.fillRect(x, y, width, height);
    graphics.lineStyle(3, 0x303335, 0.92);
    graphics.strokeRect(x, y, width, height);
    graphics.lineStyle(1, 0xffffff, 0.26);
    graphics.lineBetween(x + 8, y + 8, x + width - 8, y + 8);
  }

  private drawArenaPit(obstacle: Obstacle): void {
    const { x, y, width, height } = obstacle;
    const graphics = this.add.graphics().setDepth(y + height - 1);
    graphics.fillStyle(0x383c3f, 1);
    graphics.fillRoundedRect(x, y, width, height, 18);
    graphics.lineStyle(9, 0x74706a, 1);
    graphics.strokeRoundedRect(x, y, width, height, 18);
    graphics.lineStyle(2, 0xc5b999, 0.28);
    for (let inset = 24; inset < Math.min(width, height) / 2; inset += 28) {
      graphics.strokeRoundedRect(x + inset, y + inset, width - inset * 2, height - inset * 2, 8);
    }
  }

  private drawArenaCrate(obstacle: Obstacle, color: number): void {
    const { x, y, width, height } = obstacle;
    const graphics = this.add.graphics().setDepth(y + height);
    graphics.fillStyle(0x2b2d2e, 0.22);
    graphics.fillRect(x + 7, y + 10, width, height);
    graphics.fillStyle(color, 1);
    graphics.fillRect(x, y, width, height);
    graphics.lineStyle(3, 0x2d2f31, 0.95);
    graphics.strokeRect(x, y, width, height);
    graphics.lineStyle(2, 0xffffff, 0.2);
    graphics.lineBetween(x + 8, y + 8, x + width - 8, y + height - 8);
    graphics.lineBetween(x + width - 8, y + 8, x + 8, y + height - 8);
    if (width > 100) {
      for (let column = x + 64; column < x + width; column += 64) {
        graphics.lineStyle(3, 0x2d2f31, 0.8);
        graphics.lineBetween(column, y, column, y + height);
      }
    }
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
    this.entities.get(this.welcome.playerId)?.equipWeapon(weapon);
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
    const attackerView = this.entities.get(event.attackerId);
    const isLocalAttacker = event.attackerId === this.welcome.playerId;
    if (!isLocalAttacker) {
      attackerView?.showRecoil(
        event.weapon === "rocket" ? 9 : event.weapon === "shotgun" ? 6 : 2.5,
        event.weapon,
      );
    }
    const muzzleOffset = weaponMuzzleOffset(event.weapon, event.direction);
    const muzzle = {
      x: event.x + muzzleOffset.x,
      y: event.y + muzzleOffset.y,
    };
    if (!isLocalAttacker) {
      this.showMuzzleFlash(
        muzzle.x,
        muzzle.y,
        event.weapon,
        vector,
      );
    }

    if (event.weapon === "rocket") {
      return;
    }

    const color = event.weapon === "shotgun" ? 0xffc773 : 0xffefae;
    const width = event.weapon === "shotgun" ? 1 : 2;
    for (const trace of event.traces) {
      this.showTracer(muzzle.x, muzzle.y, trace.endX, trace.endY, color, width);
      if (trace.hit) {
        this.showImpactSpark(trace.endX, trace.endY - 40, color);
      }
    }
  }

  private showPredictedLocalAttack(
    time: number,
    input: InputPayload,
    localEntity: PlayerView | undefined,
  ): void {
    if (!input.fire || !localEntity?.canAct()) {
      return;
    }
    const weapon = input.weapon ?? this.selectedWeapon;
    const cooldown = WEAPON_COOLDOWN_MS[weapon];
    if (time - this.lastPredictedAttackAt < cooldown) {
      return;
    }
    this.lastPredictedAttackAt = time;
    const direction = localEntity.getAimDirection();
    const vector = directionVector(direction);
    const muzzle = localEntity.getMuzzlePosition();
    localEntity.showRecoil(
      weapon === "rocket" ? 9 : weapon === "shotgun" ? 6 : 2.5,
      weapon,
    );
    this.showMuzzleFlash(muzzle.x, muzzle.y, weapon, vector);
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
    glow.lineTo(endX, endY - 40);
    glow.strokePath();
    glow.lineStyle(width + 2, color, 0.35);
    glow.beginPath();
    glow.moveTo(startX, startY);
    glow.lineTo(endX, endY - 40);
    glow.strokePath();
    glow.lineStyle(width, 0xfffae8, 0.95);
    glow.beginPath();
    glow.moveTo(startX, startY);
    glow.lineTo(endX, endY - 40);
    glow.strokePath();
    glow.setBlendMode(Phaser.BlendModes.ADD);
    const angle = Math.atan2(endY - 40 - startY, endX - startX);
    const bullet = this.add
      .rectangle(startX, startY, width === 1 ? 7 : 12, 2, 0xfffae8, 1)
      .setRotation(angle)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(Math.round(startY + 235));
    const distance = Phaser.Math.Distance.Between(startX, startY, endX, endY);
    this.tweens.add({
      targets: bullet,
      x: endX,
      y: endY - 40,
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
    for (let index = 0; index < 18; index += 1) {
      const angle = (Math.PI * 2 * index) / 18 + Math.random() * 0.16;
      const size = Phaser.Math.Between(16, 36);
      const block = this.add
        .rectangle(
          x + Math.cos(angle) * 12,
          y + Math.sin(angle) * 12,
          size,
          size,
          [0xffef9c, 0xffa43d, 0xe94b32][index % 3],
          0.92,
        )
        .setRotation(angle)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(Math.round(y + 301));
      this.tweens.add({
        targets: block,
        x: x + Math.cos(angle) * Phaser.Math.Between(45, 92),
        y: y + Math.sin(angle) * Phaser.Math.Between(30, 72),
        scale: 0.15,
        alpha: 0,
        duration: Phaser.Math.Between(230, 420),
        ease: "Cubic.easeOut",
        onComplete: () => block.destroy(),
      });
    }

    const coreFlash = this.add
      .circle(x, y, 25, 0xfff9d5, 0.96)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(Math.round(y + 315));
    this.tweens.add({
      targets: coreFlash,
      scale: 3.1,
      alpha: 0,
      duration: 155,
      ease: "Cubic.easeOut",
      onComplete: () => coreFlash.destroy(),
    });

    const ringStyles = [
      { radius: 18, color: 0xfff1a1, width: 5, scale: 6.4, duration: 300 },
      { radius: 26, color: 0xff743d, width: 4, scale: 5.1, duration: 430 },
      { radius: 34, color: 0x9deaff, width: 2, scale: 4.2, duration: 560 },
    ];
    ringStyles.forEach((style, index) => {
      const ring = this.add
        .circle(x, y, style.radius, 0x000000, 0)
        .setStrokeStyle(style.width, style.color, 0.9 - index * 0.16)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(Math.round(y + 299 - index));
      this.tweens.add({
        targets: ring,
        scale: style.scale,
        alpha: 0,
        duration: style.duration,
        ease: "Cubic.easeOut",
        onComplete: () => ring.destroy(),
      });
    });

    const scorch = this.add
      .ellipse(x, y + 10, 108, 42, 0x190f0c, 0.5)
      .setStrokeStyle(3, 0x8f3b23, 0.28)
      .setDepth(Math.round(y - 2));
    this.tweens.add({
      targets: scorch,
      alpha: 0,
      duration: 7_500,
      delay: 900,
      ease: "Sine.easeIn",
      onComplete: () => scorch.destroy(),
    });

    for (let index = 0; index < 36; index += 1) {
      const angle = (Math.PI * 2 * index) / 36 + Math.random() * 0.18;
      const distance = Phaser.Math.Between(64, 165);
      const color = [0xfff2a1, 0xffbd4d, 0xff6b35, 0xdc382d][index % 4];
      const spark = this.add
        .rectangle(
          x,
          y,
          Phaser.Math.Between(5, 13),
          Phaser.Math.Between(2, 4),
          color,
          0.98,
        )
        .setRotation(angle)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(Math.round(y + 310));
      this.tweens.add({
        targets: spark,
        x: x + Math.cos(angle) * distance,
        y: y + Math.sin(angle) * distance,
        scaleX: 0.15,
        scaleY: 0.35,
        alpha: 0,
        duration: Phaser.Math.Between(260, 610),
        ease: "Quad.easeOut",
        onComplete: () => spark.destroy(),
      });
    }

    for (let index = 0; index < 14; index += 1) {
      const angle = Math.random() * Math.PI * 2;
      const ember = this.add
        .circle(x, y, Phaser.Math.Between(2, 4), 0xff8a35, 0.82)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(Math.round(y + 304));
      this.tweens.add({
        targets: ember,
        x: x + Math.cos(angle) * Phaser.Math.Between(30, 105),
        y: y + Math.sin(angle) * Phaser.Math.Between(22, 75) - Phaser.Math.Between(8, 34),
        alpha: 0,
        scale: 0.2,
        duration: Phaser.Math.Between(520, 920),
        ease: "Sine.easeOut",
        onComplete: () => ember.destroy(),
      });
    }
    this.cameras.main.flash(95, 255, 224, 157, false, undefined, this);
    this.cameras.main.shake(270, 0.009);
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

function setText(selector: string, value: string): void {
  const element = document.querySelector(selector);
  if (element) {
    element.textContent = value;
  }
}
