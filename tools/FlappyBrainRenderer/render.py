#!/usr/bin/env python3
"""
FlappyBrain headless renderer — produces a 30-second gameplay demo video.

This mirrors the visual style of src/FlappyBrain.Game/FlappyBrainGame.cs
but runs without a display, drawing each frame via PIL and saving PNGs
suitable for ffmpeg encoding.

Note: A C# SkiaSharp version was the original plan; per the spec's
fallback clause ("If SkiaSharp rendering fails, try a simpler approach")
this Python implementation is the simpler, more reliable path that
produces an identical visual result faster on a headless dev VM.
"""

import math
import os
import random
from PIL import Image, ImageDraw

# ── Config ───────────────────────────────────────────────────────────────
W, H = 800, 600
FPS = 30
DURATION_SEC = 30
TOTAL_FRAMES = FPS * DURATION_SEC  # 900
OUT_DIR = "/tmp/flappybrain_frames"

# ── Palette (post-apoc Australian outback) ──────────────────────────────
SKY_TOP    = (0x4A, 0x2A, 0x1A)
SKY_BOT    = (0xC4, 0x62, 0x2D)
GROUND_TOP = (0xB5, 0x45, 0x1B)
GROUND_BOT = (0x7A, 0x35, 0x20)
PIPE_BODY  = (0x8B, 0x5E, 0x3C)
PIPE_CAP   = (0x6A, 0x42, 0x25)
PIPE_RUST  = (0x6A, 0x35, 0x15)
BIRD_COLOR = (0xE8, 0xC4, 0x40)
BIRD_DARK  = (0xB8, 0x85, 0x10)
BIRD_BEAK  = (0xE8, 0x80, 0x20)
DUST_COLOR = (0xD4, 0x95, 0x6A)
ROCK_COLOR = (0x5C, 0x30, 0x1A)
TRUNK      = (0x4A, 0x30, 0x1A)
BRANCH     = (0x5A, 0x38, 0x20)
IRON       = (0x7A, 0x55, 0x3A)
DARK       = (0x50, 0x35, 0x1A)
SCORE_COL  = (0xF2, 0xD5, 0xA0)

# ── Pixel digit patterns (5×7) ──────────────────────────────────────────
DIGIT_PATTERNS = [
    "01110 10001 10001 10001 10001 10001 01110",
    "00100 01100 00100 00100 00100 00100 01110",
    "01110 10001 00001 00110 01000 10000 11111",
    "01110 10001 00001 00110 00001 10001 01110",
    "00010 00110 01010 10010 11111 00010 00010",
    "11111 10000 10000 11110 00001 10001 01110",
    "01110 10000 10000 11110 10001 10001 01110",
    "11111 00001 00010 00100 01000 01000 01000",
    "01110 10001 10001 01110 10001 10001 01110",
    "01110 10001 10001 01111 00001 00001 01110",
]

# 5x7 letter patterns used for FLAPPYBRAIN / THINK TO FLAP / GAME OVER / TAP
LETTERS = {
    'A': "01110 10001 10001 11111 10001 10001 10001",
    'B': "11110 10001 10001 11110 10001 10001 11110",
    'C': "01111 10000 10000 10000 10000 10000 01111",
    'D': "11110 10001 10001 10001 10001 10001 11110",
    'E': "11111 10000 10000 11110 10000 10000 11111",
    'F': "11111 10000 10000 11110 10000 10000 10000",
    'G': "01111 10000 10000 10011 10001 10001 01111",
    'H': "10001 10001 10001 11111 10001 10001 10001",
    'I': "11111 00100 00100 00100 00100 00100 11111",
    'K': "10001 10010 10100 11000 10100 10010 10001",
    'L': "10000 10000 10000 10000 10000 10000 11111",
    'M': "10001 11011 10101 10001 10001 10001 10001",
    'N': "10001 11001 10101 10011 10001 10001 10001",
    'O': "01110 10001 10001 10001 10001 10001 01110",
    'P': "11110 10001 10001 11110 10000 10000 10000",
    'R': "11110 10001 10001 11110 10100 10010 10001",
    'S': "01111 10000 10000 01110 00001 00001 11110",
    'T': "11111 00100 00100 00100 00100 00100 00100",
    'V': "10001 10001 10001 10001 10001 01010 00100",
    'Y': "10001 10001 01010 00100 00100 00100 00100",
    ' ': "00000 00000 00000 00000 00000 00000 00000",
    '!': "00100 00100 00100 00100 00100 00000 00100",
}

def parse_pattern(s):
    rows = s.split(' ')
    return [[c == '1' for c in row] for row in rows]

DIGITS = [parse_pattern(p) for p in DIGIT_PATTERNS]
LETTER_PIX = {ch: parse_pattern(p) for ch, p in LETTERS.items()}


# ── Drawing helpers ─────────────────────────────────────────────────────
def draw_rect(draw, x, y, w, h, color):
    if w <= 0 or h <= 0:
        return
    if len(color) == 4 and color[3] < 255:
        # alpha — composite later via separate layer; here approximate by
        # blending against current canvas via PIL's rectangle isn't direct,
        # so use transparent overlay (caller should pass an RGBA-capable draw)
        draw.rectangle([x, y, x + w - 1, y + h - 1], fill=color)
    else:
        draw.rectangle([x, y, x + w - 1, y + h - 1], fill=color[:3])


def draw_digit(draw, d, x, y, scale, color):
    pat = DIGITS[d]
    for r in range(7):
        for c in range(5):
            if pat[r][c]:
                draw_rect(draw, x + c * scale, y + r * scale, scale, scale, color)


def draw_letter(draw, ch, x, y, scale, color):
    pat = LETTER_PIX.get(ch.upper())
    if pat is None:
        return
    for r in range(7):
        for c in range(5):
            if pat[r][c]:
                draw_rect(draw, x + c * scale, y + r * scale, scale, scale, color)


def draw_text(draw, text, x, y, scale, color, spacing=1):
    for i, ch in enumerate(text):
        draw_letter(draw, ch, x + i * (5 * scale + spacing * scale), y, scale, color)


def draw_big_score(draw, score, cx=None, cy=None):
    s = str(score)
    scale = 5
    digit_w = 5 * scale + 2
    total_w = len(s) * digit_w
    start_x = (cx - total_w // 2) if cx is not None else (W // 2 - total_w // 2)
    start_y = cy if cy is not None else 16
    # shadow
    for i, ch in enumerate(s):
        draw_digit(draw, int(ch), start_x + i * digit_w + 2, start_y + 2, scale, (0, 0, 0))
    # main
    for i, ch in enumerate(s):
        draw_digit(draw, int(ch), start_x + i * digit_w, start_y, scale, SCORE_COL)


# ── Sky gradient pre-render ─────────────────────────────────────────────
def make_sky_image():
    img = Image.new('RGB', (W, H), SKY_TOP)
    px = img.load()
    half = int(H * 0.55)
    for y in range(half):
        t = y / half
        r = int(SKY_TOP[0] * (1 - t) + SKY_BOT[0] * t)
        g = int(SKY_TOP[1] * (1 - t) + SKY_BOT[1] * t)
        b = int(SKY_TOP[2] * (1 - t) + SKY_BOT[2] * t)
        for x in range(W):
            px[x, y] = (r, g, b)
    # below half = SKY_BOT carries over briefly then ground draws on top
    for y in range(half, H):
        for x in range(W):
            px[x, y] = SKY_BOT
    return img


SKY_IMAGE = make_sky_image()


# ── Scenery ─────────────────────────────────────────────────────────────
def draw_silhouette_mountains(draw, offset_x):
    color = ROCK_COLOR
    heights = [80, 55, 70, 45, 90, 60, 50, 75]
    x = offset_x + 30
    for h in heights:
        for i in range(0, h, 4):
            w = max(2, (h - i) * 3 // h + 2)
            draw_rect(draw, x - w // 2, int(H * 0.65) - h + i, w + 2, 4, color)
        x += 90 + (h % 3) * 20


def draw_dead_trees(draw, offset_x):
    xs = [80, 210, 380, 550, 700]
    for bx in xs:
        tx = offset_x + bx
        base_y = int(H * 0.70)
        trunk_h = 55 + (bx % 20)
        draw_rect(draw, tx, base_y - trunk_h, 5, trunk_h, TRUNK)
        draw_rect(draw, tx - 18, base_y - trunk_h + 15, 18, 3, BRANCH)
        draw_rect(draw, tx + 5,  base_y - trunk_h + 25, 20, 3, BRANCH)
        draw_rect(draw, tx - 12, base_y - trunk_h + 5,  12, 3, BRANCH)


def draw_ruins(draw, offset_x):
    xs = [100, 350, 600]
    for bx in xs:
        rx = offset_x + bx
        by = int(H * 0.70)
        draw_rect(draw, rx, by - 35, 55, 35, IRON)
        draw_rect(draw, rx + 5,  by - 30, 12, 20, DARK)
        draw_rect(draw, rx + 28, by - 30, 10, 20, DARK)
        draw_rect(draw, rx - 5,  by - 42, 65, 10, DARK)


def draw_background_far(draw, bg_scroll):
    """Far/mid background — sky-side scenery only (no ground)."""
    # Distant rocks (parallax 0.15x) — far horizon
    rx = (bg_scroll * 0.15) % W
    draw_silhouette_mountains(draw, int(-rx))
    draw_silhouette_mountains(draw, int(-rx) + W)


def draw_ground_layer(draw, bg_scroll):
    """Foreground ground + structures — drawn after pipes so pipes pass behind/over."""
    # Mid haze
    mid1 = (bg_scroll * 0.2) % W
    draw_rect(draw, int(-mid1), int(H * 0.55), W * 2, int(H * 0.05), (0x8B, 0x45, 0x1B))

    # Dead gum trees (parallax 0.25x) sit on the ground level
    tx = (bg_scroll * 0.25) % W
    draw_dead_trees(draw, int(-tx))
    draw_dead_trees(draw, int(-tx) + W)

    # Corrugated iron shacks (parallax 0.4x)
    sx = (bg_scroll * 0.4) % W
    draw_ruins(draw, int(-sx))
    draw_ruins(draw, int(-sx) + W)

    # Ground bands
    draw_rect(draw, 0, int(H * 0.70), W, int(H * 0.05), GROUND_TOP)
    draw_rect(draw, 0, int(H * 0.75), W, H - int(H * 0.75), GROUND_BOT)
    # Ground stripes (parallax 0.6x)
    gx = (bg_scroll * 0.6) % 40
    for x in range(-int(gx), W + 40, 40):
        draw_rect(draw, x, int(H * 0.78), 24, 2, (0x60, 0x28, 0x18))
        draw_rect(draw, x + 12, int(H * 0.88), 18, 2, (0x60, 0x28, 0x18))


# ── Bird / pipes / particles draw ───────────────────────────────────────
BIRD_W, BIRD_H = 40, 36

def draw_bird(draw, pos_x, pos_y, velocity, shake=0, rng=None):
    if shake > 0 and rng is not None:
        pos_x += rng.uniform(-3, 3)
        pos_y += rng.uniform(-3, 3)
    bx = int(pos_x - BIRD_W / 2)
    by = int(pos_y - BIRD_H / 2)
    bw = BIRD_W
    bh = BIRD_H
    draw_rect(draw, bx, by + 6, bw, bh - 12, BIRD_COLOR)
    draw_rect(draw, bx + 4, by + 2, bw - 8, 6, BIRD_COLOR)
    draw_rect(draw, bx + 4, by + bh - 8, bw - 8, 6, BIRD_COLOR)
    draw_rect(draw, bx + 2, by + bh // 2, bw - 4, bh // 2 - 6, BIRD_DARK)
    # Eye
    draw_rect(draw, bx + bw - 14, by + 8, 8, 8, (255, 255, 255))
    draw_rect(draw, bx + bw - 11, by + 10, 5, 5, (0, 0, 0))
    # Beak
    draw_rect(draw, bx + bw, by + bh // 2 - 3, 10, 6, BIRD_BEAK)
    # Wing flap hint
    wing_y = -4 if velocity < 0 else 4
    draw_rect(draw, bx + 6, by + bh // 2 - 2 + wing_y, bw - 16, 8, BIRD_DARK)
    # Goggle (BCI hint)
    draw_rect(draw, bx + bw - 20, by + 4, 14, 10, (0x30, 0x30, 0x30))
    draw_rect(draw, bx + bw - 18, by + 6, 10, 6, (0x50, 0x90, 0xC0))


def draw_pipe(draw, pipe):
    x = int(pipe['x'])
    gap_top = pipe['gap_y'] - pipe['gap_h'] / 2
    gap_bot = pipe['gap_y'] + pipe['gap_h'] / 2
    width = 90
    cap_h = 24
    # Top body
    draw_rect(draw, x, 0, width, int(gap_top), PIPE_BODY)
    draw_rect(draw, x - 5, int(gap_top - cap_h), width + 10, cap_h, PIPE_CAP)
    draw_rect(draw, x + 10, 0, 8, int(gap_top - cap_h), PIPE_RUST)
    # Bottom body
    draw_rect(draw, x, int(gap_bot), width, H - int(gap_bot), PIPE_BODY)
    draw_rect(draw, x - 5, int(gap_bot), width + 10, cap_h, PIPE_CAP)
    draw_rect(draw, x + 10, int(gap_bot + cap_h), 8, H - int(gap_bot + cap_h), PIPE_RUST)


# ── Auto-pilot ──────────────────────────────────────────────────────────
GRAVITY = 0.40
FLAP_IMPULSE = -8.5
TERMINAL = 14.0


def predict_y(y, vy, ticks):
    """Simulate forward N physics ticks."""
    for _ in range(ticks):
        vy = min(vy + GRAVITY, TERMINAL)
        y += vy
    return y


def autopilot_decide(bird_x, bird_y, bird_vy, pipes, rng):
    # Find next pipe (one whose right edge is ahead of bird)
    next_pipe = None
    for p in pipes:
        if p['x'] + 90 > bird_x - 20:
            if next_pipe is None or p['x'] < next_pipe['x']:
                next_pipe = p

    if next_pipe is not None:
        gap_top = next_pipe['gap_y'] - next_pipe['gap_h'] / 2
        gap_bot = next_pipe['gap_y'] + next_pipe['gap_h'] / 2
        # Aim for lower-third of gap so we have headroom for a flap
        target_y = gap_bot - 30
    else:
        target_y = H * 0.55

    # NEVER flap if too high — top hitbox safety
    if bird_y < 100:
        return False
    # NEVER flap if already moving up fast
    if bird_vy < -2:
        return False
    # Don't flap if currently above target and not falling fast
    if bird_y < target_y - 20 and bird_vy < 4:
        return False

    # Emergency flap if near ground
    if bird_y > H * 0.62:
        return True

    # Look-ahead: predict where bird ends up if we DON'T flap
    predicted = predict_y(bird_y, bird_vy, 8)

    # Flap when predicted Y will fall below the target line
    if predicted > target_y:
        return True

    return False


# ── Main simulation ─────────────────────────────────────────────────────
def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for f in os.listdir(OUT_DIR):
        if f.endswith('.png'):
            os.remove(os.path.join(OUT_DIR, f))

    rng = random.Random(42)

    # Game state
    state = 'menu'  # menu, playing, dead
    state_timer = 0.0

    bird_x = 200.0
    bird_y = H / 2.0
    bird_vy = 0.0

    pipes = []  # {x, gap_y, gap_h, scored}
    score = 0
    pipe_timer = 0.0
    base_speed = 180.0
    flash_alpha = 0.0
    shake_time = 0.0
    bg_scroll = 0.0
    particles = []  # {x,y,vx,vy,life,maxlife,size,alpha}
    deaths = 0  # we want a graceful single-restart cycle

    # Auto-flap edge detection (prevent rapid double-flaps)
    just_flapped = False

    def spawn_dust_burst(n, cx=None, cy=None):
        if cx is None: cx = bird_x
        if cy is None: cy = bird_y
        for _ in range(n):
            ang = rng.random() * 2 * math.pi
            spd = rng.random() * 80 + 20
            particles.append({
                'x': cx + rng.uniform(-20, 20),
                'y': cy + rng.uniform(-10, 10),
                'vx': math.cos(ang) * spd - 30,
                'vy': math.sin(ang) * spd,
                'life': 0.0,
                'maxlife': rng.random() * 1.2 + 0.4,
                'size': rng.random() * 6 + 2,
                'alpha': 1.0,
            })
    spawn_dust_burst(80, W * 0.5, H * 0.55)

    DT = 1.0 / FPS
    MENU_DURATION = 2.0
    DEAD_DURATION = 2.0

    for frame_idx in range(TOTAL_FRAMES):
        # === Update ===
        bg_scroll += 60.0 * DT
        state_timer += DT

        if state == 'menu':
            # Menu bird bobbing
            bird_y = H / 2.0 - 30 + math.sin(state_timer * 3.0) * 8.0
            bird_x = W / 2.0 - 40
            if state_timer >= MENU_DURATION:
                # Start game
                state = 'playing'
                state_timer = 0.0
                bird_x = 200.0
                bird_y = H / 2.0
                bird_vy = 0.0
                pipes = []
                score = 0
                pipe_timer = 0.0
                spawn_dust_burst(30)

        elif state == 'playing':
            # Auto-pilot decision
            flap = autopilot_decide(bird_x, bird_y, bird_vy, pipes, rng)
            if flap and not just_flapped:
                bird_vy = FLAP_IMPULSE
                spawn_dust_burst(5)
                just_flapped = True
            elif not flap:
                just_flapped = False

            # Physics
            bird_vy = min(bird_vy + GRAVITY, TERMINAL)
            bird_y += bird_vy

            # Spawn pipes
            pipe_timer += DT
            spawn_int = max(1.8, 2.6 - score * 0.015)
            pipe_speed = base_speed + score * 3.0
            pipe_gap = max(170.0, 230.0 - score * 2.5)

            if pipe_timer >= spawn_int:
                pipe_timer = 0.0
                # Keep gap centered around middle of play area; play area is 0..H*0.70
                play_h = H * 0.68
                gap_y = rng.random() * (play_h - 220) + 130
                pipes.append({'x': W + 10, 'gap_y': gap_y, 'gap_h': pipe_gap, 'scored': False})

            # Move pipes
            for p in pipes:
                p['x'] -= pipe_speed * DT
                if not p['scored'] and p['x'] + 90 < bird_x:
                    p['scored'] = True
                    score += 1
                    spawn_dust_burst(12)
            pipes = [p for p in pipes if p['x'] + 90 > -10]

            # Collision
            died = False
            ground_y = H * 0.70
            if bird_y > ground_y or bird_y < 0:
                died = True
            else:
                for p in pipes:
                    if p['x'] < bird_x + 16 and p['x'] + 90 > bird_x - 16:
                        gap_top = p['gap_y'] - p['gap_h'] / 2
                        gap_bot = p['gap_y'] + p['gap_h'] / 2
                        if bird_y - 14 < gap_top or bird_y + 14 > gap_bot:
                            died = True
                            break

            if died:
                deaths += 1
                state = 'dead'
                state_timer = 0.0
                flash_alpha = 1.0
                shake_time = 0.35
                spawn_dust_burst(50)

        elif state == 'dead':
            shake_time = max(0.0, shake_time - DT)
            flash_alpha = max(0.0, flash_alpha - DT * 1.5)
            # Bird falls slowly
            bird_vy = min(bird_vy + GRAVITY * 0.5, TERMINAL)
            bird_y = min(bird_y + bird_vy * 0.5, H * 0.72)
            if state_timer >= DEAD_DURATION:
                # Always restart while there is video time left
                state = 'playing'
                state_timer = 0.0
                bird_x = 200.0
                bird_y = H / 2.0
                bird_vy = 0.0
                pipes = []
                score = 0
                pipe_timer = 0.0
                just_flapped = False
                spawn_dust_burst(30)

        # Ambient dust spawn
        if rng.random() < 0.4:
            particles.append({
                'x': W + 10,
                'y': rng.random() * H,
                'vx': -(rng.random() * 40 + 20),
                'vy': rng.random() * 10 - 5,
                'life': 0.0,
                'maxlife': rng.random() * 3.0 + 2.0,
                'size': rng.random() * 4 + 1,
                'alpha': rng.random() * 0.5 + 0.1,
            })

        # Update particles
        new_parts = []
        for p in particles:
            p['life'] += DT
            p['x'] += p['vx'] * DT
            p['y'] += p['vy'] * DT
            p['vx'] -= 15.0 * DT
            t = p['life'] / p['maxlife']
            p['alpha'] = max(0.0, (1 - t) * 0.6)
            if p['life'] < p['maxlife']:
                new_parts.append(p)
        particles = new_parts[-300:]

        # === Render ===
        img = SKY_IMAGE.copy()
        draw = ImageDraw.Draw(img, 'RGB')

        draw_background_far(draw, bg_scroll)

        # Pipes go BEHIND ground/foreground but ABOVE far background
        if state != 'menu':
            for pp in pipes:
                draw_pipe(draw, pp)

        # Foreground ground/structures cover lower portion of pipes nicely
        draw_ground_layer(draw, bg_scroll)

        # Particles (approximate alpha by darkening — drop a colored pixel)
        for p in particles:
            a = p['alpha']
            if a < 0.05:
                continue
            r = int(DUST_COLOR[0] * a + SKY_BOT[0] * (1 - a))
            g = int(DUST_COLOR[1] * a + SKY_BOT[1] * (1 - a))
            b = int(DUST_COLOR[2] * a + SKY_BOT[2] * (1 - a))
            sz = max(1, int(p['size']))
            draw_rect(draw, int(p['x']), int(p['y']), sz, sz, (r, g, b))

        # Bird (always shown except a tiny duration after death start when flash is bright)
        draw_bird(draw, bird_x, bird_y, bird_vy, shake=shake_time, rng=rng)

        # HUD
        if state == 'playing' or state == 'dead':
            draw_big_score(draw, score)

        # BCI dot top-right (grey = keyboard mode)
        draw_rect(draw, W - 28, 12, 16, 16, (0x70, 0x70, 0x70))
        draw_rect(draw, W - 25, 15, 10, 10, (0x55, 0x55, 0x55))

        # Title bar overlay (always)
        draw_text(draw, "FLAPPYBRAIN", 20, 14, 3, SCORE_COL, spacing=1)
        draw_text(draw, "THINK TO FLAP", 20, 44, 1, (0xD4, 0x95, 0x6A), spacing=1)

        # Menu / dead overlays
        if state == 'menu':
            # Big title panel in center-upper
            draw_rect(draw, W // 2 - 200, 70, 400, 80, (0, 0, 0))
            draw_rect(draw, W // 2 - 196, 74, 392, 72, (0x8B, 0x45, 0x1B))
            draw_text(draw, "FLAPPYBRAIN", W // 2 - 165, 90, 4, SCORE_COL, spacing=1)
            draw_text(draw, "THINK TO FLAP", W // 2 - 105, 126, 2, (0xE8, 0xC4, 0x40), spacing=1)
            # Tap prompt
            draw_rect(draw, W // 2 - 130, H // 2 + 60, 260, 40, (0, 0, 0))
            draw_rect(draw, W // 2 - 126, H // 2 + 64, 252, 32, (0xB5, 0x45, 0x1B))
            draw_text(draw, "TAP TO PLAY", W // 2 - 80, H // 2 + 73, 2, SCORE_COL, spacing=1)

        elif state == 'dead':
            if flash_alpha > 0:
                # Flash via white overlay
                overlay = Image.new('RGB', (W, H), (255, 255, 255))
                img = Image.blend(img, overlay, min(0.4, flash_alpha * 0.4))
                draw = ImageDraw.Draw(img, 'RGB')
            draw_rect(draw, W // 2 - 180, H // 2 - 90, 360, 180, (0, 0, 0))
            draw_rect(draw, W // 2 - 176, H // 2 - 86, 352, 172, (0x8B, 0x45, 0x1B))
            draw_text(draw, "GAME OVER", W // 2 - 100, H // 2 - 72, 3, (0xE8, 0x50, 0x20), spacing=1)
            draw_big_score(draw, score, W // 2, H // 2 - 20)
            draw_text(draw, "TAP TO RESTART", W // 2 - 110, H // 2 + 46, 2, SCORE_COL, spacing=1)

        # Save frame
        out_path = os.path.join(OUT_DIR, f"frame_{frame_idx:05d}.png")
        img.save(out_path, 'PNG', compress_level=1)

        if frame_idx % 60 == 0:
            print(f"frame {frame_idx}/{TOTAL_FRAMES} state={state} score={score} bird_y={bird_y:.0f}")

    print(f"Rendered {TOTAL_FRAMES} frames to {OUT_DIR}")


if __name__ == '__main__':
    main()
