import { access, cp, mkdir, rm } from "node:fs/promises";
import path from "node:path";

const output = path.resolve("client/dist");
const portal = path.resolve("portal");
const arena = path.resolve("unity-client/PrebuiltWebGL");
const sideScroller = path.resolve("unity-side-scroller/PrebuiltWebGL");

await Promise.all([portal, arena, sideScroller].map(source => access(source)));
await rm(output, { recursive: true, force: true });
await mkdir(output, { recursive: true });
await cp(portal, output, { recursive: true });
await cp(arena, path.join(output, "arena"), { recursive: true });
await cp(sideScroller, path.join(output, "side-scroller"), { recursive: true });

process.stdout.write("Web release assembled: lobby + existing arena + independent side-scroller.\n");
