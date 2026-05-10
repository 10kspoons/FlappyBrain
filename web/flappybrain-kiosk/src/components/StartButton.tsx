'use client'

import Link from 'next/link'

interface Props {
  playerName: string | null
}

export default function StartButton({ playerName }: Props) {
  const hasPlayer = !!playerName && playerName.trim().length > 0

  if (!hasPlayer) {
    return (
      <div className="flex flex-col gap-3 items-stretch">
        <div className="font-arcade text-xs text-white/50 text-center">
          NO PLAYER QUEUED
        </div>
        <Link
          href="/scan"
          className="font-arcade text-xl md:text-2xl text-bg bg-cyan-neon px-10 py-6 rounded shadow-neon-cyan hover:brightness-110 transition active:scale-95 text-center"
        >
          SCAN BADGE
        </Link>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-4 items-stretch">
      <div className="text-center">
        <div className="font-arcade text-[10px] text-white/50 mb-2">NEXT PLAYER</div>
        <div className="font-arcade text-xl md:text-3xl text-amber-hot">{playerName!.toUpperCase()}</div>
      </div>
      <Link
        href={`/game?player=${encodeURIComponent(playerName!)}`}
        className="start-pulse font-arcade text-2xl md:text-3xl text-bg bg-cyan-neon px-12 py-6 rounded text-center hover:brightness-110 transition active:scale-95"
      >
        START GAME
      </Link>
      <Link
        href="/scan"
        className="font-arcade text-sm text-cyan-neon border border-cyan-neon/50 px-6 py-3 rounded text-center hover:bg-cyan-neon/10 transition"
      >
        SCAN ANOTHER BADGE
      </Link>
    </div>
  )
}
