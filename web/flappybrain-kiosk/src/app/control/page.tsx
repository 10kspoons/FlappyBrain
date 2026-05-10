'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'

type Action = 'start' | 'pause' | 'stop' | 'train'
type Status = 'idle' | 'pending' | 'ok' | 'error'

const BUTTONS: {
  action: Action
  label: string
  icon: string
  accent: string
  hint: string
}[] = [
  {
    action: 'start',
    label: 'START GAME',
    icon: '🎮',
    accent: 'cyan',
    hint: 'Begin a new run',
  },
  {
    action: 'pause',
    label: 'PAUSE',
    icon: '⏸',
    accent: 'amber',
    hint: 'Toggle pause overlay',
  },
  {
    action: 'stop',
    label: 'STOP',
    icon: '⏹',
    accent: 'red',
    hint: 'Return to leaderboard',
  },
  {
    action: 'train',
    label: 'TRAIN / RECALIBRATE',
    icon: '🧠',
    accent: 'purple',
    hint: 'BCI calibration flow',
  },
]

const ACCENT_CLASSES: Record<string, string> = {
  cyan: 'border-cyan-neon/60 bg-cyan-neon/10 text-cyan-neon hover:bg-cyan-neon/20 hover:shadow-neon-cyan',
  amber: 'border-amber-hot/60 bg-amber-hot/10 text-amber-hot hover:bg-amber-hot/20 hover:shadow-neon-amber',
  red: 'border-red-neon/60 bg-red-neon/10 text-red-neon hover:bg-red-neon/20 hover:shadow-neon-red',
  purple: 'border-purple-400/60 bg-purple-500/10 text-purple-300 hover:bg-purple-500/20 hover:shadow-[0_0_20px_rgba(168,85,247,0.6)]',
}

export default function ControlPage() {
  const router = useRouter()
  const [ready, setReady] = useState(false)
  const [gameIp, setGameIp] = useState('localhost')
  const [ipDraft, setIpDraft] = useState('localhost')
  const [status, setStatus] = useState<Record<Action, Status>>({
    start: 'idle',
    pause: 'idle',
    stop: 'idle',
    train: 'idle',
  })
  const [lastError, setLastError] = useState<Record<Action, string | null>>({
    start: null,
    pause: null,
    stop: null,
    train: null,
  })

  useEffect(() => {
    if (typeof window === 'undefined') return
    if (window.localStorage.getItem('flappybrain_auth') !== '1') {
      router.replace('/')
      return
    }
    const stored = window.localStorage.getItem('flappybrain_game_ip') || 'localhost'
    setGameIp(stored)
    setIpDraft(stored)
    setReady(true)
  }, [router])

  function saveIp() {
    const trimmed = ipDraft.trim() || 'localhost'
    window.localStorage.setItem('flappybrain_game_ip', trimmed)
    setGameIp(trimmed)
  }

  async function fire(action: Action) {
    setStatus((s) => ({ ...s, [action]: 'pending' }))
    setLastError((e) => ({ ...e, [action]: null }))
    try {
      const res = await fetch(`http://${gameIp}:5001/control/${action}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({}),
      })
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const data = await res.json().catch(() => ({ ok: true }))
      if (data && data.ok === false) throw new Error(data.error || 'game refused')
      setStatus((s) => ({ ...s, [action]: 'ok' }))
      window.setTimeout(() => setStatus((s) => ({ ...s, [action]: 'idle' })), 1500)
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'unreachable'
      setStatus((s) => ({ ...s, [action]: 'error' }))
      setLastError((e) => ({ ...e, [action]: msg }))
      window.setTimeout(() => setStatus((s) => ({ ...s, [action]: 'idle' })), 2200)
    }
  }

  if (!ready) {
    return null
  }

  return (
    <main className='relative min-h-screen w-full flex flex-col items-center px-4 py-6 md:py-10 gap-8'>
      <header className='w-full max-w-4xl flex flex-col items-center gap-2'>
        <div className='font-arcade text-[10px] text-white/40 tracking-widest'>
          <Link href='/' className='hover:text-cyan-neon transition-colors'>← BACK</Link>
        </div>
        <h1 className='font-arcade text-2xl md:text-4xl text-cyan-neon tracking-widest text-center'>
          OPERATOR <span className='text-amber-hot'>CONSOLE</span>
        </h1>
        <p className='font-arcade text-[10px] md:text-xs text-white/50 tracking-wider text-center'>
          REMOTE GAME CONTROL — TARGET <span className='text-cyan-neon'>{gameIp}</span>:5001
        </p>
      </header>

      <section className='w-full max-w-4xl grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-6'>
        {BUTTONS.map((b) => {
          const s = status[b.action]
          return (
            <button
              key={b.action}
              onClick={() => fire(b.action)}
              disabled={s === 'pending'}
              className={`group relative flex flex-col items-center justify-center gap-3 rounded-lg border-2 px-6 py-10 md:py-14 transition-all duration-200 ${
                ACCENT_CLASSES[b.accent]
              } disabled:opacity-60 disabled:cursor-wait`}
            >
              <div className='text-5xl md:text-6xl'>{b.icon}</div>
              <div className='font-arcade text-base md:text-xl tracking-widest'>{b.label}</div>
              <div className='font-sans text-xs text-white/50'>{b.hint}</div>

              <div className='absolute top-3 right-3 font-arcade text-xs'>
                {s === 'pending' && <span className='text-white/60 animate-pulse'>...</span>}
                {s === 'ok' && <span className='text-green-400'>✅</span>}
                {s === 'error' && <span className='text-red-neon'>❌</span>}
              </div>

              {s === 'error' && lastError[b.action] && (
                <div className='absolute bottom-2 left-0 right-0 font-sans text-[10px] text-red-neon/80 text-center px-2 truncate'>
                  {lastError[b.action]}
                </div>
              )}
            </button>
          )
        })}
      </section>

      <section className='w-full max-w-4xl border border-white/10 rounded-lg p-5 bg-black/40'>
        <div className='font-arcade text-[10px] text-white/40 tracking-widest mb-3'>GAME MACHINE IP</div>
        <div className='flex flex-col sm:flex-row gap-3 items-stretch'>
          <input
            type='text'
            value={ipDraft}
            onChange={(e) => setIpDraft(e.target.value)}
            placeholder='e.g. 192.168.1.42 or localhost'
            className='flex-1 bg-black/60 border border-white/20 rounded px-3 py-2 font-mono text-sm text-white focus:outline-none focus:border-cyan-neon'
          />
          <button
            onClick={saveIp}
            className='font-arcade text-xs tracking-widest border border-cyan-neon/60 bg-cyan-neon/10 text-cyan-neon rounded px-5 py-2 hover:bg-cyan-neon/20 transition-colors'
          >
            SAVE
          </button>
        </div>
        <p className='mt-3 font-sans text-xs text-white/40'>
          Requests are sent to <span className='text-cyan-neon/80 font-mono'>http://{gameIp}:5001/control/&lt;action&gt;</span>.
          The game must be running and reachable on the local network.
        </p>
      </section>

      <footer className='mt-auto pt-6 font-arcade text-[10px] text-white/30 tracking-wider'>
        FLAPPYBRAIN OPERATOR — v1
      </footer>
    </main>
  )
}
