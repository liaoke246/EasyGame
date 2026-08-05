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
  headTop: number;
  face: number;
  torso: number;
  torsoTop: number;
  torsoSide: number;
  legs: number;
  sleeves: number;
  hands: number;
  accent: number;
}

const OUTLINE = 0x151617;

export class BlockCharacterModel {
  readonly container: Phaser.GameObjects.Container;
  readonly weaponLayer: Phaser.GameObjects.Container;

  private readonly shadow: Phaser.GameObjects.Ellipse;
  private readonly leftLeg: Phaser.GameObjects.Rectangle;
  private readonly rightLeg: Phaser.GameObjects.Rectangle;
  private readonly leftFoot: Phaser.GameObjects.Rectangle;
  private readonly rightFoot: Phaser.GameObjects.Rectangle;
  private readonly bodyLayer: Phaser.GameObjects.Container;
  private readonly torsoSide: Phaser.GameObjects.Polygon;
  private readonly torsoTop: Phaser.GameObjects.Polygon;
  private readonly torsoAccent: Phaser.GameObjects.Rectangle;
  private readonly headSide: Phaser.GameObjects.Polygon;
  private readonly headTop: Phaser.GameObjects.Polygon;
  private readonly facePanel: Phaser.GameObjects.Rectangle;
  private readonly faceMark: Phaser.GameObjects.Rectangle;
  private readonly backSleeve: Phaser.GameObjects.Rectangle;
  private readonly frontSleeve: Phaser.GameObjects.Rectangle;
  private readonly backArm: Phaser.GameObjects.Rectangle;
  private readonly frontArm: Phaser.GameObjects.Rectangle;
  private readonly backHand: Phaser.GameObjects.Rectangle;
  private readonly frontHand: Phaser.GameObjects.Rectangle;
  private readonly weaponStock: Phaser.GameObjects.Rectangle;
  private readonly weaponBody: Phaser.GameObjects.Rectangle;
  private readonly weaponTop: Phaser.GameObjects.Rectangle;
  private readonly weaponDetail: Phaser.GameObjects.Rectangle;
  private readonly weaponGrip: Phaser.GameObjects.Rectangle;
  private readonly magazine: Phaser.GameObjects.Rectangle;
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
    const modelScale =
      options.kind === "brute" ? 1.2 : options.kind === "runner" ? 0.94 : 1;

    this.shadow = scene.add.ellipse(2, 1, 34, 11, 0x141414, 0.3);
    this.leftLeg = block(scene, -5, -12, 8, 20, this.palette.legs);
    this.rightLeg = block(scene, 5, -12, 8, 20, darken(this.palette.legs, 8));
    this.leftFoot = block(scene, -5, -2, 10, 6, darken(this.palette.legs, 24));
    this.rightFoot = block(scene, 5, -2, 10, 6, darken(this.palette.legs, 30));

    const torso = block(scene, 0, -31, 24, 27, this.palette.torso);
    this.torsoSide = plane(
      scene,
      0,
      0,
      [8, -44, 12, -40, 12, -18, 8, -16],
      this.palette.torsoSide,
    );
    this.torsoTop = plane(
      scene,
      0,
      0,
      [-12, -44, -8, -48, 12, -48, 12, -40, 8, -37, -12, -40],
      this.palette.torsoTop,
    );
    this.torsoAccent = block(scene, -8, -31, 4, 21, this.palette.accent, 0);
    const belt = block(scene, 0, -19, 22, 4, darken(this.palette.torso, 22), 0);

    const head = block(scene, 0, -59, 24, 24, this.palette.head);
    this.headSide = plane(
      scene,
      0,
      0,
      [7, -70, 12, -74, 12, -49, 7, -46],
      darken(this.palette.head, 25),
    );
    this.headTop = plane(
      scene,
      0,
      0,
      [-12, -71, -7, -76, 12, -76, 12, -71, 7, -67, -12, -67],
      this.palette.headTop,
    );
    this.facePanel = block(scene, 0, -52, 14, 5, this.palette.face, 0);
    this.faceMark = block(scene, 0, -52, 3, 5, darken(this.palette.face, 38), 0);
    this.hitFlash = block(scene, 0, -46, 27, 57, 0xffffff, 0);
    this.hitFlash.setAlpha(0);
    this.bodyLayer = scene.add.container(0, 0, [
      torso,
      this.torsoSide,
      this.torsoTop,
      this.torsoAccent,
      belt,
      head,
      this.headSide,
      this.headTop,
      this.facePanel,
      this.faceMark,
      this.hitFlash,
    ]);

    this.backSleeve = block(scene, 7, -5, 14, 6, this.palette.sleeves);
    this.frontSleeve = block(scene, 7, 5, 14, 6, this.palette.sleeves);
    this.backSleeve.setOrigin(0.08, 0.5);
    this.frontSleeve.setOrigin(0.08, 0.5);
    this.backArm = block(scene, 17, -5, 10, 4, this.palette.hands);
    this.frontArm = block(scene, 17, 5, 10, 4, this.palette.hands);
    this.backHand = block(scene, 22, -5, 5, 5, darken(this.palette.hands, 8));
    this.frontHand = block(scene, 22, 5, 5, 5, this.palette.hands);

    this.weaponStock = block(scene, 16, 0, 10, 6, 0x292b2c);
    this.weaponBody = block(scene, 30, 0, 24, 6, 0x343738);
    this.weaponTop = block(scene, 30, -2, 21, 2, 0x717778, 0);
    this.weaponDetail = block(scene, 23, 0, 7, 8, this.palette.accent);
    this.weaponGrip = block(scene, 25, 5, 5, 10, 0x262829);
    this.weaponGrip.setRotation(-0.18);
    this.magazine = block(scene, 31, 6, 5, 10, 0x242627);
    this.magazine.setRotation(-0.12);
    this.muzzleCap = block(scene, 42, 0, 4, 8, 0x171819);
    this.weaponLayer = scene.add.container(0, PLAYER_WEAPON_PIVOT_Y, [
      this.backSleeve,
      this.backArm,
      this.backHand,
      this.weaponStock,
      this.weaponBody,
      this.weaponTop,
      this.weaponDetail,
      this.weaponGrip,
      this.magazine,
      this.muzzleCap,
      this.frontSleeve,
      this.frontArm,
      this.frontHand,
    ]);

    this.container = scene.add.container(0, 0, [
      this.shadow,
      this.weaponLayer,
      this.leftLeg,
      this.rightLeg,
      this.leftFoot,
      this.rightFoot,
      this.bodyLayer,
    ]);
    this.container.setScale(modelScale);
    this.setWeapon(options.kind === "player" ? "smg" : undefined);
    this.setFacing("down");
  }

  setFacing(direction: Direction): void {
    this.direction = direction;
    const vector = directionVector(direction);
    const angle = directionAngleDegrees(direction);
    this.weaponLayer.setAngle(angle);

    const aimingAway = vector.y < -0.2;
    if (aimingAway) {
      this.container.moveTo(this.weaponLayer, 1);
    } else {
      this.container.bringToTop(this.weaponLayer);
    }

    const sideFacing = Math.abs(vector.x) > 0.7 && Math.abs(vector.y) < 0.2;
    const diagonal = Math.abs(vector.x) > 0.2 && Math.abs(vector.y) > 0.2;
    if (direction === "up") {
      this.facePanel.setVisible(false);
      this.faceMark.setVisible(false);
    } else if (sideFacing) {
      this.facePanel
        .setVisible(true)
        .setPosition(vector.x * 10, -58)
        .setDisplaySize(5, 14);
      this.faceMark
        .setVisible(true)
        .setPosition(vector.x * 10, -58)
        .setDisplaySize(5, 3);
    } else if (diagonal) {
      this.facePanel
        .setVisible(true)
        .setPosition(vector.x * 7, -56 + Math.max(0, vector.y) * 4)
        .setDisplaySize(vector.y > 0 ? 10 : 5, vector.y > 0 ? 6 : 12);
      this.faceMark
        .setVisible(true)
        .setPosition(vector.x * 7, -55 + Math.max(0, vector.y) * 4)
        .setDisplaySize(vector.y > 0 ? 3 : 5, 3);
    } else {
      this.facePanel.setVisible(true).setPosition(0, -52).setDisplaySize(14, 5);
      this.faceMark.setVisible(true).setPosition(0, -52).setDisplaySize(3, 5);
    }

    const sideSign = vector.x === 0 ? 1 : -Math.sign(vector.x);
    this.headSide.setScale(sideSign, 1);
    this.torsoSide.setScale(sideSign, 1);
    this.headTop.setPosition(-vector.x * 1.5, -vector.y * 1.1);
    this.torsoTop.setPosition(-vector.x, -vector.y * 0.6);
  }

  setWeapon(weapon: WeaponId | undefined): void {
    this.weapon = weapon;
    const armed = weapon !== undefined;
    for (const part of [
      this.weaponStock,
      this.weaponBody,
      this.weaponTop,
      this.weaponDetail,
      this.weaponGrip,
      this.magazine,
      this.muzzleCap,
    ]) {
      part.setVisible(armed);
    }

    if (!armed) {
      this.backSleeve.setDisplaySize(14, 6);
      this.frontSleeve.setDisplaySize(14, 6);
      this.backArm.setDisplaySize(12, 4).setPosition(18, -5);
      this.frontArm.setDisplaySize(12, 4).setPosition(18, 5);
      this.backHand.setPosition(25, -5);
      this.frontHand.setPosition(25, 5);
      return;
    }

    const muzzleDistance = WEAPON_MUZZLE_DISTANCES[weapon];
    const gunStart = weapon === "rocket" ? 14 : 18;
    const gunLength = muzzleDistance - gunStart - 2;
    const gunWidth = weapon === "rocket" ? 11 : weapon === "shotgun" ? 6 : 7;
    const gunColor =
      weapon === "rocket" ? 0x596258 : weapon === "shotgun" ? 0x373334 : 0x323637;
    const highlight =
      weapon === "rocket" ? 0x8f9b89 : weapon === "shotgun" ? 0x73706f : 0x777e7f;

    this.backSleeve.setDisplaySize(13, 6);
    this.frontSleeve.setDisplaySize(13, 6);
    this.backArm.setDisplaySize(9, 4).setPosition(16, -5);
    this.frontArm.setDisplaySize(9, 4).setPosition(16, 5);
    this.backHand.setPosition(21, -5);
    this.frontHand.setPosition(21, 5);
    this.weaponStock
      .setFillStyle(weapon === "shotgun" ? 0x6a432d : 0x292b2c)
      .setDisplaySize(weapon === "rocket" ? 12 : 10, weapon === "rocket" ? 12 : 6)
      .setPosition(weapon === "rocket" ? 14 : 16, 0);
    this.weaponBody
      .setFillStyle(gunColor)
      .setDisplaySize(gunLength, gunWidth)
      .setPosition(gunStart + gunLength / 2, 0);
    this.weaponTop
      .setFillStyle(highlight)
      .setDisplaySize(Math.max(8, gunLength - 3), 2)
      .setPosition(gunStart + gunLength / 2, -gunWidth / 2 + 1);
    this.weaponDetail
      .setFillStyle(weapon === "rocket" ? 0xb43a31 : weapon === "shotgun" ? 0x875638 : this.palette.accent)
      .setDisplaySize(weapon === "rocket" ? 5 : 7, gunWidth + 3)
      .setPosition(weapon === "rocket" ? 30 : 23, 0);
    this.weaponGrip
      .setVisible(weapon !== "rocket")
      .setPosition(weapon === "shotgun" ? 27 : 24, 5);
    this.magazine
      .setVisible(weapon === "smg")
      .setPosition(31, 6);
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
    const vector = directionVector(this.direction);
    const perpendicular = { x: -vector.y, y: vector.x };
    const stride = wave * 4.1;
    const bodyBob = moving ? -Math.abs(wave) * 0.75 : 0;
    const legAngle = directionAngleDegrees(this.direction) - 90;

    this.leftLeg
      .setPosition(
        perpendicular.x * 5 + vector.x * stride * 0.34,
        -11 + perpendicular.y * 3 + vector.y * stride * 0.34,
      )
      .setAngle(legAngle);
    this.rightLeg
      .setPosition(
        -perpendicular.x * 5 - vector.x * stride * 0.34,
        -11 - perpendicular.y * 3 - vector.y * stride * 0.34,
      )
      .setAngle(legAngle);
    this.leftFoot
      .setPosition(
        perpendicular.x * 5 + vector.x * stride,
        -2 + perpendicular.y * 3 + vector.y * stride,
      )
      .setAngle(legAngle);
    this.rightFoot
      .setPosition(
        -perpendicular.x * 5 - vector.x * stride,
        -2 - perpendicular.y * 3 - vector.y * stride,
      )
      .setAngle(legAngle);
    this.bodyLayer.y = bodyBob + bodyOffsetY;

    this.weaponLayer.setPosition(
      -vector.x * recoil * 0.45,
      PLAYER_WEAPON_PIVOT_Y + bodyBob + bodyOffsetY - vector.y * recoil * 0.45,
    );
    this.shadow.setScale(1 - Math.abs(wave) * 0.025, 1);
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
    this.hitFlash.setAlpha(0.72);
    this.scene.tweens.add({
      targets: this.hitFlash,
      alpha: 0,
      duration: 85,
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
  strokeWidth = 1,
): Phaser.GameObjects.Rectangle {
  return scene.add
    .rectangle(x, y, width, height, color, 1)
    .setStrokeStyle(strokeWidth, OUTLINE, strokeWidth === 0 ? 0 : 0.95);
}

function plane(
  scene: Phaser.Scene,
  x: number,
  y: number,
  points: number[],
  color: number,
): Phaser.GameObjects.Polygon {
  return scene.add
    .polygon(x, y, points, color, 1)
    .setOrigin(0, 0)
    .setStrokeStyle(1, OUTLINE, 0.88);
}

function paletteFor(options: BlockCharacterOptions): Palette {
  if (options.kind === "player") {
    const accent = parseColor(options.accentColor, 0x4c956c);
    return {
      head: 0x202325,
      headTop: 0x44494b,
      face: 0xc7865d,
      torso: 0x242729,
      torsoTop: 0x4b5051,
      torsoSide: 0x151718,
      legs: 0x25282a,
      sleeves: 0x303436,
      hands: 0xc7865d,
      accent,
    };
  }
  if (options.kind === "runner") {
    return {
      head: 0x686d6d,
      headTop: 0x8d9290,
      face: 0x942b29,
      torso: 0x8f302d,
      torsoTop: 0xc15046,
      torsoSide: 0x5a201e,
      legs: 0x3e4242,
      sleeves: 0x963531,
      hands: 0x3a3c3b,
      accent: 0xd14a3f,
    };
  }
  if (options.kind === "brute") {
    return {
      head: 0x424748,
      headTop: 0x686e6e,
      face: 0xa02e2a,
      torso: 0x3d4142,
      torsoTop: 0x646a6a,
      torsoSide: 0x282b2c,
      legs: 0x292c2d,
      sleeves: 0x4a4f4f,
      hands: 0x242627,
      accent: 0xb63631,
    };
  }
  return {
    head: 0x747979,
    headTop: 0xa5aaaa,
    face: 0x9b2c2a,
    torso: 0xe1e0da,
    torsoTop: 0xffffff,
    torsoSide: 0x979b9b,
    legs: 0x565a5b,
    sleeves: 0xe7e6df,
    hands: 0x373a3a,
    accent: 0xb93431,
  };
}

function parseColor(value: string | undefined, fallback: number): number {
  if (!value) {
    return fallback;
  }
  const parsed = Number.parseInt(value.replace("#", ""), 16);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function darken(color: number, amount: number): number {
  const red = Phaser.Math.Clamp(((color >> 16) & 0xff) - amount, 0, 255);
  const green = Phaser.Math.Clamp(((color >> 8) & 0xff) - amount, 0, 255);
  const blue = Phaser.Math.Clamp((color & 0xff) - amount, 0, 255);
  return (red << 16) | (green << 8) | blue;
}
