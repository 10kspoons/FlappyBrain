'use client'

import { Suspense, useEffect, useState, useCallback } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import FlappyGame, { type GameTheme } from '@/components/FlappyGame'

function GameInner() {
  const router = useRouter()
  const params = useSearchParams()
  const queryName = params.get('player')
  const queryTheme = params.get('theme') as GameTheme | null
  const [playerName, setPlayerName] = useState<string | null>(queryName || null)
  const [loading, setLoading] = useState(!queryName)
  const [error, setError] = useState<string>('')

  useEffect(() => {
    if (queryName) return
    let cancelled = false
    ;(async () => {
      try {
        const res = await fetch('/api/current-player', { cache: 'no-store' })
        const data = (await res.json()) as { name: string | null }
        if (cancelled) return
        if (!data.name) {
          setError('No player queued — scan a badge first')
        } else {
          setPlayerName(data.name)
        }
      } catch (err) {
        if (cancelled) return
        setError(err instanceof Error ? err.message : 'Failed to load player')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [queryName])

  const handleEnd = useCallback(
    async (score: number, sectionsCompleted: number) => {
      const name = playerName || 'Anonymous'
      try {
        await fetch('/api/scores', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ playerName: name, score, sectionsCompleted }),
        })
      } catch (e) {
        console.error('Failed to save score', e)
      }
      try {
        await fetch('/api/current-player', {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ name: null }),
        })
      } catch (e) {
        console.error('Failed to clear player', e)
      }
      router.push('/?played=1')
      router.refresh()
    },
    [playerName, router]
  )

  if (loading) {
    return (
      <main className="min-h-screen w-full flex items-center justify-center">
        <div className="font-arcade text-cyan-neon text-sm">LOADING…</div>
      </main>
    )
  }

  if (error || !playerName) {
    return (
      <main className="min-h-screen w-full flex flex-col items-center justify-center gap-4 p-6">
        <div className="font-arcade text-red-neon text-base text-center">
          {error || 'No player queued'}
        </div>
        <button
          onClick={() => router.push('/')}
          className="font-arcade text-sm text-cyan-neon border border-cyan-neon px-6 py-3 rounded hover:bg-cyan-neon/10 transition"
        >
          BACK TO KIOSK
        </button>
      </main>
    )
  }

  return <FlappyGame playerName={playerName} theme={queryTheme ?? 'neon'} onGameEnd={handleEnd} />
}

export default function GamePage() {
  return (
    <Suspense
      fallback={
        <main className="min-h-screen w-full flex items-center justify-center">
          <div className="font-arcade text-cyan-neon text-sm">LOADING…</div>
        </main>
      }
    >
      <GameInner />
    </Suspense>
  )
}
