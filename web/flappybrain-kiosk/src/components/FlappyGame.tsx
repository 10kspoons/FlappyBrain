'use client'

import { useEffect, useRef, useState } from 'react'

export type GameTheme = 'neon' | 'outback'

interface FlappyGameProps {
  playerName: string
  theme?: GameTheme
  onGameEnd: (score: number, sectionsCompleted: number) => void
}

type GameState = 'IDLE' | 'PLAYING' | 'DEAD'

interface Pipe {
  x: number
  gapY: number
  gapHeight: number
  width: number
  passed: boolean
}

const GRAVITY = 0.25
const FLAP = -7.0
const TERMINAL = 12
const KOALA_RADIUS = 18
const PIPE_WIDTH = 80
const BASE_GAP = 200
const SPAWN_INTERVAL_MS = 2500
const BASE_SCROLL_SPEED = 3.2

export default function FlappyGame({ playerName, theme = 'neon', onGameEnd }: FlappyGameProps) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const containerRef = useRef<HTMLDivElement | null>(null)
  const [state, setState] = useState<GameState>('IDLE')
  const [score, setScore] = useState(0)
  const stateRef = useRef<GameState>('IDLE')
  const scoreRef = useRef(0)
  const sectionsRef = useRef(0)
  const onGameEndRef = useRef(onGameEnd)

  useEffect(() => {
    onGameEndRef.current = onGameEnd
  }, [onGameEnd])

  useEffect(() => {
    stateRef.current = state
  }, [state])

  useEffect(() => {
    const canvas = canvasRef.current
    const container = containerRef.current
    if (!canvas || !container) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    let width = 0
    let height = 0
    const resize = () => {
      const rect = container.getBoundingClientRect()
      const dpr = window.devicePixelRatio || 1
      width = rect.width
      height = rect.height
      canvas.width = Math.floor(width * dpr)
      canvas.height = Math.floor(height * dpr)
      canvas.style.width = `${width}px`
      canvas.style.height = `${height}px`
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
    }
    resize()
    window.addEventListener('resize', resize)

    let koalaY = 0
    let koalaVy = 0
    const pipes: Pipe[] = []
    let lastSpawnAt = 0
    let lastFrameAt = 0
    let frameId = 0
    let running = true
    let deadAt = 0
    let starsCache: { x: number; y: number; r: number; tw: number }[] | null = null

    const reset = () => {
      koalaY = height / 2
      koalaVy = 0
      pipes.length = 0
      lastSpawnAt = performance.now()
      scoreRef.current = 0
      sectionsRef.current = 0
      setScore(0)
    }

    const flap = () => {
      const s = stateRef.current
      if (s === 'IDLE') {
        reset()
        stateRef.current = 'PLAYING'
        setState('PLAYING')
        koalaVy = FLAP
        return
      }
      if (s === 'PLAYING') {
        koalaVy = FLAP
      }
    }

    const onPointer = (e: Event) => {
      e.preventDefault()
      flap()
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.code === 'Space' || e.code === 'ArrowUp' || e.code === 'Enter') {
        e.preventDefault()
        flap()
      }
    }
    canvas.addEventListener('pointerdown', onPointer)
    window.addEventListener('keydown', onKey)

    reset()
    stateRef.current = 'IDLE'
    setState('IDLE')

    const ensureStars = () => {
      if (starsCache && starsCache.length > 0) return starsCache
      const arr: { x: number; y: number; r: number; tw: number }[] = []
      const count = Math.floor((width * height) / 8000)
      for (let i = 0; i < count; i++) {
        arr.push({
          x: Math.random() * width,
          y: Math.random() * height * 0.7,
          r: Math.random() * 1.4 + 0.2,
          tw: Math.random() * Math.PI * 2,
        })
      }
      starsCache = arr
      return arr
    }

    const drawBackground = (t: number) => {
      const grd = ctx.createLinearGradient(0, 0, 0, height)
      if (theme === 'outback') {
        grd.addColorStop(0, '#1a0500')
        grd.addColorStop(0.55, '#4A2A1A')
        grd.addColorStop(1, '#C4622D')
      } else {
        grd.addColorStop(0, '#03030a')
        grd.addColorStop(0.55, '#0b0f2a')
        grd.addColorStop(1, '#1a0a2a')
      }
      ctx.fillStyle = grd
      ctx.fillRect(0, 0, width, height)

      const stars = ensureStars()
      if (theme === 'outback') {
        const drift = (t * 0.02) % width
        for (const s of stars) {
          const a = 0.25 + 0.25 * Math.sin(t * 0.001 + s.tw)
          ctx.fillStyle = `rgba(210,160,80,${a.toFixed(3)})`
          ctx.beginPath()
          const x = (s.x - drift + width) % width
          ctx.ellipse(x, s.y, s.r * 1.6, s.r * 0.9, 0, 0, Math.PI * 2)
          ctx.fill()
        }
      } else {
        for (const s of stars) {
          const a = 0.45 + 0.4 * Math.sin(t * 0.002 + s.tw)
          ctx.fillStyle = `rgba(255,255,255,${a.toFixed(3)})`
          ctx.beginPath()
          ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2)
          ctx.fill()
        }
      }

      const horizon = height * 0.78
      if (theme === 'outback') {
        ctx.fillStyle = '#3D1F0A'
        ctx.fillRect(0, horizon, width, height - horizon)
        ctx.strokeStyle = 'rgba(200,120,40,0.4)'
      } else {
        ctx.fillStyle = '#0d0612'
        ctx.fillRect(0, horizon, width, height - horizon)
        ctx.strokeStyle = 'rgba(0,255,229,0.18)'
      }
      ctx.lineWidth = 1
      ctx.beginPath()
      ctx.moveTo(0, horizon)
      ctx.lineTo(width, horizon)
      ctx.stroke()
    }

    const drawScanlines = () => {
      ctx.save()
      ctx.globalAlpha = 0.06
      ctx.fillStyle = '#000'
      for (let y = 0; y < height; y += 3) {
        ctx.fillRect(0, y, width, 1)
      }
      ctx.restore()
    }

    const drawKoala = (x: number, y: number, vy: number) => {
      const tilt = Math.max(-0.45, Math.min(0.9, vy * 0.05))
      ctx.save()
      ctx.translate(x, y)
      ctx.rotate(tilt)

      const bodyColour = theme === 'outback' ? '#C8A860' : '#ffb800'
      const earDark = theme === 'outback' ? '#1a0a00' : '#0a0a0f'

      ctx.fillStyle = bodyColour
      ctx.shadowBlur = 18
      ctx.shadowColor = bodyColour
      ctx.beginPath()
      ctx.arc(0, 0, KOALA_RADIUS, 0, Math.PI * 2)
      ctx.fill()
      ctx.shadowBlur = 0

      ctx.fillStyle = earDark
      ctx.beginPath()
      ctx.arc(-KOALA_RADIUS * 0.7, -KOALA_RADIUS * 0.6, KOALA_RADIUS * 0.55, 0, Math.PI * 2)
      ctx.arc(KOALA_RADIUS * 0.7, -KOALA_RADIUS * 0.6, KOALA_RADIUS * 0.55, 0, Math.PI * 2)
      ctx.fill()
      ctx.fillStyle = bodyColour
      ctx.beginPath()
      ctx.arc(-KOALA_RADIUS * 0.7, -KOALA_RADIUS * 0.6, KOALA_RADIUS * 0.3, 0, Math.PI * 2)
      ctx.arc(KOALA_RADIUS * 0.7, -KOALA_RADIUS * 0.6, KOALA_RADIUS * 0.3, 0, Math.PI * 2)
      ctx.fill()

      if (theme === 'outback') {
        ctx.fillStyle = 'rgba(80,200,255,0.7)'
        ctx.beginPath()
        ctx.arc(-KOALA_RADIUS * 0.3, -2, 4, 0, Math.PI * 2)
        ctx.arc(KOALA_RADIUS * 0.3, -2, 4, 0, Math.PI * 2)
        ctx.fill()
        ctx.strokeStyle = '#000'
        ctx.lineWidth = 1.2
        ctx.beginPath()
        ctx.arc(-KOALA_RADIUS * 0.3, -2, 4, 0, Math.PI * 2)
        ctx.stroke()
        ctx.beginPath()
        ctx.arc(KOALA_RADIUS * 0.3, -2, 4, 0, Math.PI * 2)
        ctx.stroke()
        ctx.beginPath()
        ctx.moveTo(-KOALA_RADIUS * 0.3 + 4, -2)
        ctx.lineTo(KOALA_RADIUS * 0.3 - 4, -2)
        ctx.stroke()

        ctx.fillStyle = '#1a0a00'
        ctx.beginPath()
        ctx.ellipse(0, KOALA_RADIUS * 0.35, 4, 3, 0, 0, Math.PI * 2)
        ctx.fill()
      } else {
        ctx.fillStyle = '#0a0a0f'
        ctx.beginPath()
        ctx.arc(KOALA_RADIUS * 0.5, -2, 3.2, 0, Math.PI * 2)
        ctx.fill()
        ctx.fillStyle = '#fff'
        ctx.beginPath()
        ctx.arc(KOALA_RADIUS * 0.55, -2.5, 1.2, 0, Math.PI * 2)
        ctx.fill()

        ctx.fillStyle = '#0a0a0f'
        ctx.beginPath()
        ctx.ellipse(KOALA_RADIUS * 0.85, KOALA_RADIUS * 0.25, 4, 3, 0, 0, Math.PI * 2)
        ctx.fill()
      }

      ctx.restore()
    }

    const drawPipe = (p: Pipe) => {
      ctx.save()
      if (theme === 'outback') {
        ctx.shadowBlur = 12
        ctx.shadowColor = '#C4622D'
        ctx.fillStyle = '#2A1505'
        ctx.strokeStyle = '#A0522D'
      } else {
        ctx.shadowBlur = 12
        ctx.shadowColor = '#00ffe5'
        ctx.fillStyle = '#0a1a1a'
        ctx.strokeStyle = '#00ffe5'
      }
      ctx.lineWidth = 2

      const topH = p.gapY
      ctx.fillRect(p.x, 0, p.width, topH)
      ctx.strokeRect(p.x + 0.5, 0.5, p.width - 1, topH - 1)
      if (theme === 'outback') {
        ctx.fillRect(p.x - 6, topH - 20, p.width + 12, 20)
        ctx.strokeRect(p.x - 5.5, topH - 19.5, p.width + 11, 19)
      } else {
        ctx.fillRect(p.x - 4, topH - 18, p.width + 8, 18)
        ctx.strokeRect(p.x - 3.5, topH - 17.5, p.width + 7, 17)
      }

      const botY = p.gapY + p.gapHeight
      const botH = height - botY
      ctx.fillRect(p.x, botY, p.width, botH)
      ctx.strokeRect(p.x + 0.5, botY + 0.5, p.width - 1, botH - 1)
      if (theme === 'outback') {
        ctx.fillRect(p.x - 6, botY, p.width + 12, 20)
        ctx.strokeRect(p.x - 5.5, botY + 0.5, p.width + 11, 19)
      } else {
        ctx.fillRect(p.x - 4, botY, p.width + 8, 18)
        ctx.strokeRect(p.x - 3.5, botY + 0.5, p.width + 7, 17)
      }

      ctx.restore()
    }

    const drawIdleOverlay = (t: number) => {
      ctx.save()
      ctx.fillStyle = 'rgba(0,0,0,0.45)'
      ctx.fillRect(0, 0, width, height)

      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'

      const primary = theme === 'outback' ? '#E8AA40' : '#00ffe5'
      const accent = theme === 'outback' ? '#FF6B35' : '#ffb800'
      const tapShadow = theme === 'outback' ? '#E8AA40' : '#00ffe5'

      ctx.fillStyle = primary
      ctx.shadowBlur = 18
      ctx.shadowColor = primary
      ctx.font = '20px "Press Start 2P", monospace'
      ctx.fillText('READY', width / 2, height * 0.32)

      ctx.font = '28px "Press Start 2P", monospace'
      ctx.fillStyle = accent
      ctx.shadowColor = accent
      const name = playerName.toUpperCase()
      ctx.fillText(name, width / 2, height * 0.42)

      const flash = 0.5 + 0.5 * Math.sin(t * 0.005)
      ctx.globalAlpha = 0.4 + 0.6 * flash
      ctx.fillStyle = '#ffffff'
      ctx.shadowColor = tapShadow
      ctx.font = '18px "Press Start 2P", monospace'
      ctx.fillText('TAP TO FLAP', width / 2, height * 0.58)
      ctx.globalAlpha = 1

      ctx.restore()
    }

    const drawDeadOverlay = () => {
      ctx.save()
      ctx.fillStyle = 'rgba(0,0,0,0.6)'
      ctx.fillRect(0, 0, width, height)

      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'
      const overColour = theme === 'outback' ? '#FF4500' : '#ff2d55'
      const overShadow = theme === 'outback' ? '#E8AA40' : '#ff2d55'
      const scoreColour = theme === 'outback' ? '#E8AA40' : '#00ffe5'
      const nameColour = theme === 'outback' ? '#C8A860' : '#ffb800'

      ctx.fillStyle = overColour
      ctx.shadowBlur = 22
      ctx.shadowColor = overShadow
      ctx.font = '36px "Press Start 2P", monospace'
      ctx.fillText('GAME OVER', width / 2, height * 0.38)

      ctx.fillStyle = scoreColour
      ctx.shadowColor = scoreColour
      ctx.font = '22px "Press Start 2P", monospace'
      ctx.fillText(`SCORE  ${scoreRef.current}`, width / 2, height * 0.5)

      ctx.fillStyle = nameColour
      ctx.shadowColor = nameColour
      ctx.font = '14px "Press Start 2P", monospace'
      ctx.fillText(playerName.toUpperCase(), width / 2, height * 0.6)
      ctx.restore()
    }

    const drawScore = () => {
      ctx.save()
      const c = theme === 'outback' ? '#E8AA40' : '#00ffe5'
      ctx.fillStyle = c
      ctx.shadowBlur = 12
      ctx.shadowColor = c
      ctx.font = '28px "Press Start 2P", monospace'
      ctx.textAlign = 'right'
      ctx.textBaseline = 'top'
      ctx.fillText(String(scoreRef.current), width - 20, 20)
      ctx.restore()
    }

    const spawnPipe = () => {
      const minGap = 140
      const reduce = Math.min(60, scoreRef.current * 1.5)
      const gapHeight = Math.max(minGap, BASE_GAP - reduce)
      const margin = 60
      const gapY = margin + Math.random() * (height - gapHeight - margin * 2)
      pipes.push({
        x: width + 20,
        gapY,
        gapHeight,
        width: PIPE_WIDTH,
        passed: false,
      })
    }

    const collide = (kx: number, ky: number) => {
      if (ky - KOALA_RADIUS < 0) return true
      if (ky + KOALA_RADIUS > height - 4) return true
      for (const p of pipes) {
        const inX = kx + KOALA_RADIUS > p.x && kx - KOALA_RADIUS < p.x + p.width
        if (!inX) continue
        const inGap = ky - KOALA_RADIUS > p.gapY && ky + KOALA_RADIUS < p.gapY + p.gapHeight
        if (!inGap) return true
      }
      return false
    }

    const tick = (t: number) => {
      if (!running) return
      if (lastFrameAt === 0) lastFrameAt = t
      const dt = Math.min(48, t - lastFrameAt)
      lastFrameAt = t
      const stepScale = dt / (1000 / 60)

      drawBackground(t)

      const kx = width * 0.3
      const speed = BASE_SCROLL_SPEED + Math.min(3.5, scoreRef.current * 0.08)

      if (stateRef.current === 'PLAYING') {
        koalaVy = Math.min(TERMINAL, koalaVy + GRAVITY * stepScale)
        koalaY += koalaVy * stepScale

        if (t - lastSpawnAt > SPAWN_INTERVAL_MS) {
          spawnPipe()
          lastSpawnAt = t
        }

        for (const p of pipes) {
          p.x -= speed * stepScale
          if (!p.passed && p.x + p.width < kx - KOALA_RADIUS) {
            p.passed = true
            scoreRef.current += 1
            setScore(scoreRef.current)
          }
        }
        while (pipes.length && pipes[0].x + pipes[0].width + 40 < 0) pipes.shift()

        if (collide(kx, koalaY)) {
          stateRef.current = 'DEAD'
          setState('DEAD')
          deadAt = t
        }
      } else if (stateRef.current === 'IDLE') {
        koalaY = height / 2 + Math.sin(t * 0.004) * 14
      }

      for (const p of pipes) drawPipe(p)
      drawKoala(kx, koalaY, koalaVy)

      if (stateRef.current === 'PLAYING') drawScore()
      if (stateRef.current === 'IDLE') drawIdleOverlay(t)
      if (stateRef.current === 'DEAD') {
        drawScore()
        drawDeadOverlay()
        if (t - deadAt > 2000) {
          running = false
          onGameEndRef.current(scoreRef.current, sectionsRef.current)
          return
        }
      }

      drawScanlines()
      frameId = requestAnimationFrame(tick)
    }

    frameId = requestAnimationFrame(tick)

    return () => {
      running = false
      cancelAnimationFrame(frameId)
      window.removeEventListener('resize', resize)
      canvas.removeEventListener('pointerdown', onPointer)
      window.removeEventListener('keydown', onKey)
    }
  }, [playerName, theme])

  return (
    <div ref={containerRef} className="relative w-screen h-screen bg-black overflow-hidden">
      <canvas ref={canvasRef} className="block w-full h-full touch-none select-none" />
      <div className="pointer-events-none absolute top-4 left-4 font-arcade text-xs text-cyan-neon opacity-80">
        {playerName.toUpperCase()}
      </div>
      <div className="pointer-events-none absolute bottom-4 left-4 font-arcade text-[10px] text-white/40">
        FLAPPYBRAIN — STATE: {state}
      </div>
    </div>
  )
}
