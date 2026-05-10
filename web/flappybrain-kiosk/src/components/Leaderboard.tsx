import type { Score } from '@/lib/db'

interface LeaderboardProps {
  top10: Score[]
  recent: Score | null
}

const RANK_STYLES = [
  'text-yellow-300 shadow-neon-gold border-yellow-300/60',
  'text-gray-200 shadow-neon-silver border-gray-200/60',
  'text-orange-400 shadow-neon-bronze border-orange-400/60',
]

function formatTimestamp(iso: string | null | undefined): string {
  if (!iso) return ''
  const dateLike = iso.includes('T') ? iso : iso.replace(' ', 'T') + 'Z'
  const d = new Date(dateLike)
  if (Number.isNaN(d.getTime())) return ''
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function Row({
  rank,
  name,
  score,
  highlight,
  ts,
}: {
  rank: number | string
  name: string
  score: number
  highlight?: string
  ts?: string | null
}) {
  const isPodium = typeof rank === 'number' && rank <= 3
  const podiumClass = isPodium ? RANK_STYLES[(rank as number) - 1] : 'text-cyan-neon border-cyan-neon/30'
  return (
    <div
      className={`flex items-center justify-between gap-4 px-4 py-3 rounded border ${podiumClass} ${
        highlight ? 'bg-white/5' : 'bg-black/40'
      } ${isPodium ? 'shadow-lg' : ''}`}
    >
      <div className="flex items-center gap-4 min-w-0">
        <span className="font-arcade text-base w-12 text-right tabular-nums">{rank}</span>
        <span className="font-arcade text-sm md:text-base truncate text-white">{name}</span>
      </div>
      <div className="flex items-center gap-3">
        {ts && <span className="font-sans text-xs text-white/40">{ts}</span>}
        <span className="font-arcade text-base md:text-lg text-cyan-neon tabular-nums">{score}</span>
      </div>
    </div>
  )
}

export default function Leaderboard({ top10, recent }: LeaderboardProps) {
  const recentInTop = recent ? top10.some((s) => s.id === recent.id) : false

  return (
    <section className="w-full max-w-2xl flex flex-col gap-3">
      <div className="flex items-center justify-between mb-1">
        <h2 className="font-arcade text-lg text-cyan-neon">LEADERBOARD</h2>
        <span className="font-arcade text-[10px] text-white/40">TOP 10</span>
      </div>

      <div className="flex flex-col gap-2">
        {top10.length === 0 && (
          <div className="font-arcade text-xs text-white/40 px-4 py-6 text-center border border-white/10 rounded">
            NO SCORES YET — BE THE FIRST
          </div>
        )}
        {top10.map((s, i) => (
          <Row key={s.id} rank={i + 1} name={s.player_name} score={s.score} ts={formatTimestamp(s.played_at)} />
        ))}
      </div>

      {recent && !recentInTop && (
        <>
          <div className="flex items-center gap-3 mt-2">
            <span className="flex-1 h-px bg-amber-hot/30" />
            <span className="font-arcade text-[10px] text-amber-hot">MOST RECENT</span>
            <span className="flex-1 h-px bg-amber-hot/30" />
          </div>
          <div className="border border-amber-hot/50 rounded bg-amber-hot/5">
            <Row
              rank="—"
              name={recent.player_name}
              score={recent.score}
              highlight="recent"
              ts={formatTimestamp(recent.played_at)}
            />
          </div>
        </>
      )}
    </section>
  )
}
