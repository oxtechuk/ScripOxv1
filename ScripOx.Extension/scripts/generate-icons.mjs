/**
 * Generate ScripOx extension icons as PNG using SVG → Canvas → PNG
 * Works with Node.js built-ins only (no external dependencies).
 * 
 * Strategy: Write SVG to a temp HTML file, render it with a headless
 * approach, OR generate a valid minimal PNG binary directly.
 * 
 * Here we generate a compact but valid PNG manually for each size.
 */

import { writeFileSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import zlib from 'node:zlib';

const __dir = dirname(fileURLToPath(import.meta.url));
const OUT   = join(__dir, '..', 'public', 'icons');
const OUT2  = join(__dir, '..', 'dist',   'icons');
mkdirSync(OUT,  { recursive: true });
mkdirSync(OUT2, { recursive: true });

// ── PNG writer helpers ───────────────────────────────────────

function crc32(buf) {
  let crc = -1;
  const table = makeCrcTable();
  for (let i = 0; i < buf.length; i++) {
    crc = (crc >>> 8) ^ table[(crc ^ buf[i]) & 0xff];
  }
  return (crc ^ -1) >>> 0;
}

let _crcTable = null;
function makeCrcTable() {
  if (_crcTable) return _crcTable;
  _crcTable = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) {
      c = (c & 1) ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    }
    _crcTable[n] = c;
  }
  return _crcTable;
}

function chunk(type, data) {
  const typeBytes = Buffer.from(type, 'ascii');
  const lenBuf    = Buffer.alloc(4);
  lenBuf.writeUInt32BE(data.length, 0);
  const crcBuf    = Buffer.alloc(4);
  const payload   = Buffer.concat([typeBytes, data]);
  crcBuf.writeUInt32BE(crc32(payload), 0);
  return Buffer.concat([lenBuf, typeBytes, data, crcBuf]);
}

/** Build a valid RGBA PNG from a pixel callback */
function buildPNG(size, pixelFn) {
  // IHDR
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8]  = 8;  // bit depth
  ihdr[9]  = 2;  // colour type: RGB
  ihdr[10] = 0;
  ihdr[11] = 0;
  ihdr[12] = 0;

  // Raw scanlines: filter byte 0 + RGB per pixel
  const scanline = size * 3 + 1;
  const raw      = Buffer.alloc(scanline * size);
  for (let y = 0; y < size; y++) {
    raw[y * scanline] = 0; // filter: None
    for (let x = 0; x < size; x++) {
      const [r, g, b] = pixelFn(x, y, size);
      const off = y * scanline + 1 + x * 3;
      raw[off]     = r;
      raw[off + 1] = g;
      raw[off + 2] = b;
    }
  }

  const compressed = zlib.deflateSync(raw, { level: 6 });

  return Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), // PNG signature
    chunk('IHDR', ihdr),
    chunk('IDAT', compressed),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

// ── Icon design ──────────────────────────────────────────────

function lerp(a, b, t) { return a + (b - a) * t; }
function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }

function ellipseHit(px, py, cx, cy, rx, ry) {
  const dx = (px - cx) / rx;
  const dy = (py - cy) / ry;
  return dx * dx + dy * dy <= 1;
}

function iconPixel(px, py, size) {
  const cx = size / 2;
  const cy = size / 2;
  const r  = size / 2;
  const s  = size / 128; // scale factor

  // Normalise to [-64, 64] coordinate space
  const x = (px - cx) / s;
  const y = (py - cy) / s;

  // Background: dark navy circle
  const distFromCenter = Math.sqrt((px - cx) ** 2 + (py - cy) ** 2);
  if (distFromCenter > r - 0.5) return [15, 23, 42]; // outside → background colour

  // Dark background gradient
  const t   = distFromCenter / r;
  const bgR = Math.round(lerp(30, 15, t));
  const bgG = Math.round(lerp(58, 23, t));
  const bgB = Math.round(lerp(95, 42, t));
  let [pr, pg, pb] = [bgR, bgG, bgB];

  // Blue accents for scorpion silhouette
  const BLUE   = [59,  130, 246];
  const LBLUE  = [147, 197, 253];

  // Head
  if (ellipseHit(x, y, 0, -16, 10, 12)) [pr, pg, pb] = LBLUE;

  // Body
  if (ellipseHit(x, y, 0, 8, 14, 22)) [pr, pg, pb] = BLUE;

  // Left claw
  if (ellipseHit(x, y, -22, -20, 9, 5)) [pr, pg, pb] = BLUE;

  // Right claw
  if (ellipseHit(x, y, 22, -20, 9, 5)) [pr, pg, pb] = BLUE;

  // Tail: curved strip using bezier proximity
  const tailDist = minDistToTail(x, y);
  if (tailDist < 4.5) {
    const ta = clamp(1 - tailDist / 4.5, 0, 1);
    pr = Math.round(pr * (1 - ta) + LBLUE[0] * ta);
    pg = Math.round(pg * (1 - ta) + LBLUE[1] * ta);
    pb = Math.round(pb * (1 - ta) + LBLUE[2] * ta);
  }

  // Stinger
  if (ellipseHit(x, y, 22, -36, 5, 8)) [pr, pg, pb] = [191, 219, 254];

  // Eyes (only at larger sizes)
  if (size >= 48) {
    if (ellipseHit(x, y, -4, -18, 2, 2)) [pr, pg, pb] = [15, 23, 42];
    if (ellipseHit(x, y,  4, -18, 2, 2)) [pr, pg, pb] = [15, 23, 42];
  }

  return [pr, pg, pb];
}

/**
 * Approximate distance from point to the scorpion tail bezier curve.
 * Samples 30 points along the curve.
 */
function minDistToTail(x, y) {
  let minD = Infinity;
  const pts = tailPoints();
  for (const [tx, ty] of pts) {
    const d = Math.sqrt((x - tx) ** 2 + (y - ty) ** 2);
    if (d < minD) minD = d;
  }
  return minD;
}

let _tailCache = null;
function tailPoints() {
  if (_tailCache) return _tailCache;
  const STEPS = 40;
  _tailCache  = [];
  for (let i = 0; i <= STEPS; i++) {
    const tt = i / STEPS;
    const [bx, by] = cubicBezier(
      0, 30,    // start (body base)
      10, 28,
      20, 14,
      20, 10    // mid-tail
    , tt);
    _tailCache.push([bx, by]);
  }
  for (let i = 0; i <= STEPS; i++) {
    const tt = i / STEPS;
    const [bx, by] = cubicBezier(
      20, 10,
      25, 8,
      30, -8,
      30, -18
    , tt);
    _tailCache.push([bx, by]);
  }
  for (let i = 0; i <= STEPS; i++) {
    const tt = i / STEPS;
    const [bx, by] = cubicBezier(
      30, -18,
      28, -28,
      18, -36,
      22, -32
    , tt);
    _tailCache.push([bx, by]);
  }
  return _tailCache;
}

function cubicBezier(x0, y0, x1, y1, x2, y2, x3, y3, t) {
  const mt = 1 - t;
  const x  = mt**3*x0 + 3*mt**2*t*x1 + 3*mt*t**2*x2 + t**3*x3;
  const y  = mt**3*y0 + 3*mt**2*t*y1 + 3*mt*t**2*y2 + t**3*y3;
  return [x, y];
}

// ── Generate all sizes ────────────────────────────────────────

const SIZES = [16, 32, 48, 128];

for (const size of SIZES) {
  const png  = buildPNG(size, iconPixel);
  const name = `icon${size}.png`;
  writeFileSync(join(OUT,  name), png);
  writeFileSync(join(OUT2, name), png);
  console.log(`✓ ${name} — ${png.length} bytes`);
}

console.log('\n🦂 ScripOx icons generated successfully!');
