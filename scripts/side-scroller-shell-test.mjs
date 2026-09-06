import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { test } from "node:test";
import vm from "node:vm";

const template = await readFile(new URL("../unity-side-scroller/Assets/WebGLTemplates/SideScroller/index.html", import.meta.url), "utf8");
const source = template.match(/<script>([\s\S]*?)<\/script>/)[1]
  .replace(/\{\{\{\s*JSON\.stringify\((\w+)\)\s*\}\}\}/g, (_, key) => JSON.stringify(key))
  .replace(/\{\{\{\s*(\w+)\s*\}\}\}/g, (_, key) => key);

class Element {
  constructor() {
    this.listeners = new Map();
    this.attributes = new Map();
    this.captured = new Set();
    this.classes = new Set();
    this.children = [];
    this.dataset = {};
    this.hidden = false;
    this.value = "";
    this.style = { setProperty: (key, value) => { this.style[key] = value; } };
    this.classList = {
      add: value => this.classes.add(value),
      toggle: (value, selected) => selected ? this.classes.add(value) : this.classes.delete(value),
    };
  }
  addEventListener(type, listener) {
    if (!this.listeners.has(type)) this.listeners.set(type, []);
    this.listeners.get(type).push(listener);
  }
  emit(type, properties = {}) {
    const event = { pointerId: 1, pointerType: "touch", button: 0, ...properties, preventDefault() { this.defaultPrevented = true; } };
    for (const listener of this.listeners.get(type) || []) listener(event);
    return event;
  }
  setAttribute(key, value) { this.attributes.set(key, value); }
  appendChild(child) { this.children.push(child); }
  focus() { this.focused = true; }
  remove() { this.removed = true; }
  setPointerCapture(id) { this.captured.add(id); }
  hasPointerCapture(id) { return this.captured.has(id); }
  releasePointerCapture(id) {
    if (this.captured.delete(id)) this.emit("lostpointercapture", { pointerId: id });
  }
}

function page({ url = "https://game.example/side-scroller/", storageDenied = false, historyDenied = false, instantiate } = {}) {
  const ids = ["unity-canvas", "profile-setup", "profile-form", "player-name", "loader", "progress", "status", "retry-load", "left", "right", "jump", "attack"];
  const elements = Object.fromEntries(ids.map(id => [id, new Element()]));
  elements["retry-load"].hidden = true;
  elements.loader.hidden = true;
  const avatars = ["warrior", "female", "slime"].map(avatar => {
    const element = new Element();
    element.dataset.avatar = avatar;
    return element;
  });
  const document = Object.assign(new Element(), {
    body: new Element(), documentElement: new Element(),
    querySelector: selector => elements[selector.slice(1)],
    querySelectorAll: () => avatars,
    createElement: () => new Element(),
  });
  const window = Object.assign(new Element(), {
    innerHeight: 720,
    location: { href: url, reload() { this.reloaded = true; }, assign(value) { this.assigned = value; } },
    visualViewport: Object.assign(new Element(), { height: 720, offsetTop: 0 }),
  });
  const preferences = new Map();
  const messages = [];
  const timers = new Map();
  let timerId = 0;
  const context = vm.createContext({
    window, document, URL, console,
    localStorage: {
      getItem(key) { if (storageDenied) throw new Error("Storage blocked"); return preferences.get(key); },
      setItem(key, value) { if (storageDenied) throw new Error("Storage blocked"); preferences.set(key, value); },
    },
    history: { replaceState(_state, _unused, nextUrl) { if (historyDenied) throw new Error("History blocked"); window.location.href = nextUrl.href; } },
    createUnityInstance: instantiate || (() => Promise.resolve({ SendMessage: (...args) => messages.push(args) })),
    KeyboardEvent: class { constructor() { throw new Error("Mobile controls must not synthesize duplicate keyboard input"); } },
    setTimeout: (callback, delay) => { timers.set(++timerId, { callback, delay }); return timerId; },
    clearTimeout: id => timers.delete(id),
  });
  vm.runInContext(source, context, { filename: "SideScroller/index.html" });
  return {
    elements, avatars, document, window, messages, timers, preferences,
    submit: () => elements["profile-form"].emit("submit"),
    async ready() { this.submit(); await document.body.children[0].onload(); },
    controls: () => messages.map(([_object, _method, payload]) => payload),
  };
}

test("entry survives rejected local storage, sanitizes a URL profile and starts only once", async () => {
  const app = page({ storageDenied: true, url: "https://game.example/side-scroller/?playerName=%3Cb%3EALICE%3C%2Fb%3E&avatar=unknown" });
  assert.equal(app.elements["player-name"].value, "bALICEb");
  assert.equal(app.avatars[0].attributes.get("aria-checked"), "true");
  app.elements["player-name"].value = "  ALICE-01  ";
  app.avatars[1].emit("click");
  await app.ready();
  app.submit();
  assert.equal(app.document.body.children.length, 1);
  assert.equal(new URL(app.window.location.href).searchParams.get("playerName"), "ALICE-01");
  assert.equal(new URL(app.window.location.href).searchParams.get("avatar"), "female");
  assert.equal(app.document.body.classes.has("game-ready"), true);
});

test("empty profile cannot begin; blocked history navigates with the selected profile", () => {
  const app = page({ historyDenied: true });
  app.elements["player-name"].value = "    ";
  app.submit();
  assert.equal(app.document.body.children.length, 0);
  app.elements["player-name"].value = "BOB";
  app.avatars[2].emit("click");
  app.submit();
  const destination = new URL(app.window.location.assigned);
  assert.equal(destination.searchParams.get("playerName"), "BOB");
  assert.equal(destination.searchParams.get("avatar"), "slime");
  assert.equal(destination.searchParams.get("autostart"), "1");
});

test("loader download failure provides a working reload action", () => {
  const app = page();
  app.submit();
  app.document.body.children[0].onerror();
  assert.equal(app.elements["retry-load"].hidden, false);
  assert.ok(app.elements.status.textContent.includes("未能下载"));
  assert.equal(app.timers.size, 0);
  app.elements["retry-load"].emit("click");
  assert.equal(app.window.location.reloaded, true);
});

test("both synchronous and asynchronous Unity startup failures become visible", async () => {
  for (const instantiate of [() => { throw new Error("WebGL unavailable"); }, () => Promise.reject(new Error("Wasm memory exhausted"))]) {
    const app = page({ instantiate });
    app.submit();
    await app.document.body.children[0].onload();
    assert.equal(app.elements["retry-load"].hidden, false);
    assert.equal(app.document.body.classes.has("game-ready"), false);
    assert.ok(app.elements.status.textContent.startsWith("加载失败："));
    assert.equal(app.timers.size, 0);
  }
});

test("stalled downloads offer recovery and viewport follows Safari visible size", () => {
  const app = page();
  app.submit();
  const watchdog = [...app.timers.values()].find(timer => timer.delay === 90000);
  watchdog.callback();
  assert.equal(app.elements["retry-load"].hidden, false);
  app.window.visualViewport.height = 312;
  app.window.visualViewport.offsetTop = 24;
  app.window.visualViewport.emit("resize");
  assert.equal(app.document.documentElement.style["--app-height"], "312px");
  assert.equal(app.document.documentElement.style["--app-top"], "24px");
});

test("mobile multitouch uses one input route and does not release a second finger", async () => {
  const app = page();
  app.elements.left.emit("pointerdown");
  assert.deepEqual(app.controls(), []);
  await app.ready();
  app.elements.left.emit("pointerdown", { pointerId: 1 });
  app.elements.left.emit("pointerdown", { pointerId: 2 });
  app.elements.jump.emit("pointerdown", { pointerId: 3 });
  app.elements.left.emit("pointerleave", { pointerId: 1 });
  app.elements.left.emit("pointerup", { pointerId: 1 });
  assert.deepEqual(app.controls(), ["left:1", "jump:1"]);
  assert.equal(app.elements.left.hasPointerCapture(2), true);
  app.elements.left.emit("pointercancel", { pointerId: 2 });
  app.elements.jump.releasePointerCapture(3);
  assert.deepEqual(app.controls(), ["left:1", "jump:1", "left:0", "jump:0"]);
});

test("blur, hidden tabs and page exits cancel held movement and queued actions", async () => {
  const app = page();
  await app.ready();
  for (const trigger of [() => app.window.emit("blur"), () => { app.document.hidden = true; app.document.emit("visibilitychange"); }, () => app.window.emit("pagehide")]) {
    app.messages.length = 0;
    app.elements.right.emit("pointerdown", { pointerId: 7 });
    app.elements.attack.emit("pointerdown", { pointerId: 8 });
    trigger();
    app.elements.right.emit("pointerup", { pointerId: 7 });
    assert.deepEqual(app.controls(), ["right:1", "attack:1", "right:0", "attack:0", "reset:1"]);
    assert.equal(app.elements.right.captured.size, 0);
    assert.equal(app.elements.attack.captured.size, 0);
  }
});
