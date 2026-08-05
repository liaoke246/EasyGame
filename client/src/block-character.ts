import Phaser from "phaser";
import {
  PLAYER_WEAPON_PIVOT_Y,
  WEAPON_MUZZLE_DISTANCES,
  directionAngleDegrees,
  directionVector,
  weaponMuzzleOffset,
} from "@easygame/shared";
import type { Direction, WeaponId, ZombieKind } from "./types";

type BlockCharacterKind = "player" | ZombieKind;

interface BlockCharacterOptions {
  kind: BlockCharacterKind;
  accentColor?: string;
}

interface Palette {
  head: number;
  face: number;
  torso: number;
  torsoLight: number;
  legs: number;
  arms: number;
  hands: number;
  accent: number;
}

const OUTLINE = 0x17191a;

export class BlockCharacterModel {
  readonly container: Phaser.GameObjects.Container;
  readonly weaponLayer: Phaser.GameObjects.Container;

  private readonly shadow: Phaser.GameObjects.Ellipse;
  private readonly leftLeg: Phaser.GameObjects.Rectangle;
  private readonly rightLeg: Phaser.GameObjects.Rectangle;
  private readonly leftFoot: Phaser.GameObjects.Rectangle;
  private readonly rightFoot: Phaser.GameObjects.Rectangle;
  private readonly bodyLayer: Phaser.GameObjects.Container;
  private readonly torso: Phaser.GameObjects.Rectangle;
  private readonly torsoLight: Phaser.GameObjects.Rectangle;
  private readonly head: Phaser.GameObjects.Rectangle;
  private readonly headTop: Phaser.GameObjects.Rectangle;
  private readonly facePanel: Phaser.GameObjects.Rectangle;
  private readonly backArm: Phaser.GameObjects.Rectangle;
  private readonly frontArm: Phaser.GameObjects.Rectangle;
  private readonly backHand: Phaser.GameObjects.Rectangle;
  private readonly frontHand: Phaser.GameObjects.Rectangle;
  private readonly weaponBody: Phaser.GameObjects.Rectangle;
  private readonly weaponDetail: Phaser.GameObjects.Rectangle;
  private readonly muzzleCap: Phaser.GameObjects.Rectangle;
  private readonly hitFlash: Phaser.GameObjects.Rectangle;
  private readonly palette: Palette;
  private direction: Direction = "down";
  private weapon: WeaponId | undefined;

  constructor(
    private readonly scene: Phaser.Scene,
    options: BlockCharacterOptions,
  ) {
    this.palette = paletteFor(options);
    const scale =
      options.kind === "brute" ? 1.24 : options.kind === "runner" ? 0.92 : 1;

    this.shadow = scene.add.ellipse(0, 1, 38, 13, 0x171717, 0.28);
    this.leftLeg = block(scene, -7, -14, 10, 23, this.palette.legs);
    this.rightLeg = block(scene, 7, -14, 10, 23, this.palette.legs);
    this.leftFoot = block(scene, -7, -2, 12, 7, darken(this.palette.legs, 30));
    this.rightFoot = block(scene, 7, -2, 12, 7, darken(this.palette.legs, 30));

    this.torso = block(scene, 0, -38, 30, 34, this.palette.torso);
    this.torsoLight = block(scene, -9, -43, 6, 22, this.palette.torsoLight);
    this.head = block(scene, 0, -65, 28, 26, this.palette.head);
    this.headTop = block(scene, 0, -76, 24, 5, lighten(this.palette.head, 24));
    this.facePanel = block(scene, 0, -58, 17, 7, this.palette.face, 1);
    this.hitFlash = block(scene, 0, -48, 34, 58, 0xffffff, 0);
    this.hitFlash.setAlpha(0);
    this.bodyLayer = scene.add.container(0, 0, [
      this.torso,
      this.torsoLight,
      this.head,
      this.headTop,
      this.facePanel,
      this.hitFlash,
    ]);

    this.backArm = block(scene, 11, -8, 27, 8, this.palette.arms);
    this.frontArm = block(scene, 11, 8, 27, 8, this.palette.arms);
    this.backArm.setOrigin(0.08, 0.5);
    this.frontArm.setOrigin(0.08, 0.5);
    this.backHand = block(scene, 25, -8, 8, 8, this.palette.hands);
    this.frontHand = block(scene, 25, 8, 8, 8, this.palette.hands);
    this.weaponBody = block(scene, 30, 0, 32, 7, 0x383d40);
    this.weaponDetail = block(scene, 24, 0, 9, 10, this.palette.accent);
    this.muzzleCap = block(scene, 43, 0, 5, 9, 0x1c1e20);
    this.weaponLayer = scene.add.container(0, PLAYER_WEAPON_PIVOT_Y, [
      this.backArm,
      this.backHand,
      this.weaponBody,
      this.weaponDetail,
      this.muzzleCap,
      this.frontArm,
      this.frontHand,
    ]);

    this.container = scene.add.container(0, 0, [
      this.shadow,
      this.leftLeg,
      this.rightLeg,
      this.leftFoot,
      this.rightFoot,
      this.bodyLayer,
      this.weaponLayer,
    ]);
    this.container.setScale(scale);
    this.setWeapon(options.kind === "player" ? "smg" : undefined);
    this.setFacing("down");
  }

  setFacing(direction: Direction): void {
    this.direction = direction;
    const vector = directionVector(direction);
    const radians = (directionAngleDegrees(direction) * Math.PI) / 180;
    this.weaponLayer.setRotation(radians);
    this.facePanel.setPosition(vector.x * 7, -65 + vector.y * 7);
    this.facePanel.setRotation(radians + Math.PI / 2);
    this.headTop.setPosition(-vector.x * 2, -76 - vector.y * 1.5);
  }

  setWeapon(weapon: WeaponId | undefined): void {
    this.weapon = weapon;
    const armed = weapon !== undefined;
    this.weaponBody.setVisible(armed);
    this.weaponDetail.setVisible(armed);
    this.muzzleCap.setVisible(armed);
    if (!armed) {
      this.backArm.setFillStyle(this.palette.hands);
      this.frontArm.setFillStyle(this.palette.hands);
      this.backArm.setDisplaySize(25, 8);
      this.frontArm.setDisplaySize(25, 8);
      this.backHand.setPosition(26, -8);
      this.frontHand.setPosition(26, 8);
      return;
    }

    this.backArm.setFillStyle(this.palette.arms);
    this.frontArm.setFillStyle(this.palette.arms);
    const muzzleDistance = WEAPON_MUZZLE_DISTANCES[weapon];
    const gunStart = 16;
    const gunLength = muzzleDistance - gunStart - 2;
    const gunWidth = weapon === "rocket" ? 12 : weapon === "shotgun" ? 8 : 6;
    const gunColor =
      weapon === "rocket" ? 0x66725d : weapon === "shotgun" ? 0x4d3b2d : 0x30363a;
    this.weaponBody
      .setFillStyle(gunColor)
      .setDisplaySize(gunLength, gunWidth)
      .setPosition(gunStart + gunLength / 2, 0);
    this.weaponDetail
      .setFillStyle(weapon === "rocket" ? 0xb34c39 : this.palette.accent)
      .setDisplaySize(weapon === "rocket" ? 6 : 8, gunWidth + 4)
      .setPosition(weapon === "rocket" ? 34 : 22, 0);
    this.muzzleCap
      .setDisplaySize(4, gunWidth + 2)
      .setPosition(muzzleDistance - 1, 0);
  }

  setMotion(
    walkPhase: number,
    moving: boolean,
    recoil: number,
    bodyOffsetY = 0,
  ): void {
    const wave = moving ? Math.sin(walkPhase * Math.PI * 2) : 0;
    const bounce = moving ? -Math.abs(wave) * 1.4 : 0;
    const leftAdvance = wave * 4.5;
    const rightAdvance = -wave * 4.5;
    this.leftLeg.setPosition(-7, -14 + leftAdvance * 0.42);
    this.rightLeg.setPosition(7, -14 + rightAdvance * 0.42);
    this.leftFoot.setPosition(-7, -2 + leftAdvance);
    this.rightFoot.setPosition(7, -2 + rightAdvance);
    this.bodyLayer.y = bounce + bodyOffsetY;

    const vector = directionVector(this.direction);
    this.weaponLayer.setPosition(
      -vector.x * recoil * 0.5,
      PLAYER_WEAPON_PIVOT_Y + bounce + bodyOffsetY - vector.y * recoil * 0.5,
    );
    this.shadow.setScale(1 - Math.abs(wave) * 0.035, 1);
  }

  getMuzzleLocal(): { x: number; y: number } {
    if (!this.weapon) {
      return { x: 0, y: PLAYER_WEAPON_PIVOT_Y };
    }
    const base = weaponMuzzleOffset(this.weapon, this.direction);
    return {
      x: base.x + this.weaponLayer.x,
      y: base.y + (this.weaponLayer.y - PLAYER_WEAPON_PIVOT_Y),
    };
  }

  flashDamage(): void {
    this.scene.tweens.killTweensOf(this.hitFlash);
    this.hitFlash.setAlpha(0.8);
    this.scene.tweens.add({
      targets: this.hitFlash,
      alpha: 0,
      duration: 90,
      ease: "Quad.easeOut",
    });
  }
}

function block(
  scene: Phaser.Scene,
  x: number,
  y: number,
  width: number,
  height: number,
  color: number,
  strokeWidth = 2,
): Phaser.GameObjects.Rectangle {
  return scene.add
    .rectangle(x, y, width, height, color, 1)
    .setStrokeStyle(strokeWidth, OUTLINE, 0.92);
}

function paletteFor(options: BlockCharacterOptions): Palette {
  if (options.kind === "player") {
    const accent = parseColor(options.accentColor, 0x4c956c);
    return {
      head: 0x24282c,
      face: 0xc58a62,
      torso: 0x252a2e,
      torsoLight: lighten(accent, 14),
      legs: 0x30363a,
      arms: 0x343a3e,
      hands: 0xc58a62,
      accent,
    };
  }
  if (options.kind === "runner") {
    return {
      head: 0x777d72,
      face: 0xaa3e35,
      torso: 0x8f3933,
      torsoLight: 0xb65a48,
      legs: 0x4d504b,
      arms: 0x8f3933,
      hands: 0x7d8678,
      accent: 0xc84c3e,
    };
  }
  if (options.kind === "brute") {
    return {
      head: 0x596157,
      face: 0x8f312b,
      torso: 0x454a4e,
      torsoLight: 0x697076,
      legs: 0x303437,
      arms: 0x545b58,
      hands: 0x667064,
      accent: 0xb13b32,
    };
  }
  return {
    head: 0x7a8276,
    face: 0x9f352f,
    torso: 0xb4b09f,
    torsoLight: 0xd0cbb6,
    legs: 0x555954,
    arms: 0xa6a392,
    hands: 0x7b8477,
    accent: 0xb63c32,
  };
}

function parseColor(value: string | undefined, fallback: number): number {
  if (!value) {
    return fallback;
  }
  const parsed = Number.parseInt(value.replace("#", ""), 16);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function lighten(color: number, amount: number): number {
  return adjustColor(color, amount);
}

function darken(color: number, amount: number): number {
  return adjustColor(color, -amount);
}

function adjustColor(color: number, amount: number): number {
  const red = Phaser.Math.Clamp(((color >> 16) & 0xff) + amount, 0, 255);
  const green = Phaser.Math.Clamp(((color >> 8) & 0xff) + amount, 0, 255);
  const blue = Phaser.Math.Clamp((color & 0xff) + amount, 0, 255);
  return (red << 16) | (green << 8) | blue;
}
