// Generate simple programmatic PNG icons (cyan square w/ "FB" text) without
// requiring any image library. Uses a hand-rolled minimal PNG encoder.
//
// Output: public/icon-192.png and public/icon-512.png

const fs = require('node:fs')
const path = require('node:path')
const zlib = require('node:zlib')

function crc32(buf) {
  let c
  const table = []
  for (let n = 0; n < 256; n++) {
    c = n
    for (let k = 0; k < 8; k++) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
    }
    table[n] = c
  }
  let crc = 0xffffffff
  for (let i = 0; i < buf.length; i++) {
    crc = table[(crc ^ buf[i]) & 0xff] ^ (crc >>> 8)
  }
  return (crc ^ 0xffffffff) >>> 0
}

function chunk(type, data) {
  const len = Buffer.alloc(4)
  len.writeUInt32BE(data.length, 0)
  const t = Buffer.from(type, 'ascii')
  const crcBuf = Buffer.alloc(4)
  crcBuf.writeUInt32BE(crc32(Buffer.concat([t, data])), 0)
  return Buffer.concat([len, t, data, crcBuf])
}

function makePng(width, height, pixelFn) {
  const sig = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])

  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(width, 0)
  ihdr.writeUInt32BE(height, 4)
  ihdr.writeUInt8(8, 8)
  ihdr.writeUInt8(6, 9)
  ihdr.writeUInt8(0, 10)
  ihdr.writeUInt8(0, 11)
  ihdr.writeUInt8(0, 12)

  const raw = Buffer.alloc((width * 4 + 1) * height)
  let p = 0
  for (let y = 0; y < height; y++) {
    raw[p++] = 0
    for (let x = 0; x < width; x++) {
      const [r, g, b, a] = pixelFn(x, y)
      raw[p++] = r
      raw[p++] = g
      raw[p++] = b
      raw[p++] = a
    }
  }

  const compressed = zlib.deflateSync(raw, { level: 9 })

  return Buffer.concat([sig, chunk('IHDR', ihdr), chunk('IDAT', compressed), chunk('IEND', Buffer.alloc(0))])
}

// Pixel font 5x7 for letters F and B (and a couple more if needed).
// Each row is 5 bits wide, MSB = leftmost.
const FONT = {
  F: [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000],
  B: [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
}

function drawGlyph(grid, w, h, glyph, ox, oy, scale, color) {
  const rows = FONT[glyph]
  if (!rows) return
  for (let r = 0; r < 7; r++) {
    for (let c = 0; c < 5; c++) {
      const on = (rows[r] >> (4 - c)) & 1
      if (!on) continue
      for (let dy = 0; dy < scale; dy++) {
        for (let dx = 0; dx < scale; dx++) {
          const x = ox + c * scale + dx
          const y = oy + r * scale + dy
          if (x >= 0 && x < w && y >= 0 && y < h) {
            grid[y * w + x] = color
          }
        }
      }
    }
  }
}

function generate(size, outPath) {
  const w = size
  const h = size
  const grid = new Uint32Array(w * h)
  const bg = 0x0a0a0fff

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) grid[y * w + x] = bg
  }

  const cyan = 0x00ffe5ff
  const inset = Math.floor(size * 0.08)
  const radius = Math.floor(size * 0.18)
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const inX = x >= inset && x < w - inset
      const inY = y >= inset && y < h - inset
      if (!inX || !inY) continue
      const dxL = x - (inset + radius)
      const dxR = x - (w - inset - 1 - radius)
      const dyT = y - (inset + radius)
      const dyB = y - (h - inset - 1 - radius)
      let dist = -1
      if (dxL < 0 && dyT < 0) dist = Math.hypot(dxL, dyT)
      else if (dxR > 0 && dyT < 0) dist = Math.hypot(dxR, dyT)
      else if (dxL < 0 && dyB > 0) dist = Math.hypot(dxL, dyB)
      else if (dxR > 0 && dyB > 0) dist = Math.hypot(dxR, dyB)
      if (dist > radius) continue

      const isBorder =
        x === inset || x === w - inset - 1 || y === inset || y === h - inset - 1
      grid[y * w + x] = isBorder ? cyan : 0x0a141aff
    }
  }

  const scale = Math.max(2, Math.floor(size / 24))
  const glyphW = 5 * scale
  const glyphH = 7 * scale
  const gap = scale
  const totalW = glyphW * 2 + gap
  const startX = Math.floor((w - totalW) / 2)
  const startY = Math.floor((h - glyphH) / 2)
  drawGlyph(grid, w, h, 'F', startX, startY, scale, cyan)
  drawGlyph(grid, w, h, 'B', startX + glyphW + gap, startY, scale, cyan)

  const buf = makePng(w, h, (x, y) => {
    const v = grid[y * w + x]
    return [(v >>> 24) & 0xff, (v >>> 16) & 0xff, (v >>> 8) & 0xff, v & 0xff]
  })

  fs.writeFileSync(outPath, buf)
  console.log(`Wrote ${outPath} (${size}x${size}, ${buf.length} bytes)`)
}

const publicDir = path.resolve(__dirname, '..', 'public')
fs.mkdirSync(publicDir, { recursive: true })
generate(192, path.join(publicDir, 'icon-192.png'))
generate(512, path.join(publicDir, 'icon-512.png'))
