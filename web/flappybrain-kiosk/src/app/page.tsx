import Leaderboard from '@/components/Leaderboard'
import StartButton from '@/components/StartButton'
import AutoRefresh from '@/components/AutoRefresh'
import { getCurrentPlayer, getMostRecentScore, getTopScores } from '@/lib/db'

export const dynamic = 'force-dynamic'

export default function HomePage() {
  const top10 = getTopScores(10)
  const recent = getMostRecentScore()
  const player = getCurrentPlayer()

  return (
    <main className="relative min-h-screen w-full flex flex-col items-center px-4 py-6 md:py-10 gap-6">
      <AutoRefresh intervalMs={10000} />

      <header className="w-full max-w-5xl flex flex-col items-center gap-2">
        <h1 className="font-arcade text-3xl md:text-5xl text-cyan-neon tracking-widest text-center">
          FLAPPY<span className="text-amber-hot">BRAIN</span>
        </h1>
        <p className="font-arcade text-[10px] md:text-xs text-white/50 tracking-wider text-center">
          BCI-ADAPTED ARCADE — SCAN, FOCUS, FLAP
        </p>
      </header>

      <div className="w-full max-w-5xl grid grid-cols-1 md:grid-cols-5 gap-8 md:gap-10 items-start">
        <div className="md:col-span-3">
          <Leaderboard top10={top10} recent={recent} />
        </div>

        <aside className="md:col-span-2 flex flex-col gap-4">
          <div className="border border-cyan-neon/40 rounded p-5 md:p-6 bg-black/40">
            <StartButton playerName={player.name} />
          </div>

          <div className="border border-white/10 rounded p-4 bg-black/30">
            <div className="font-arcade text-[10px] text-white/40 mb-2">HOW IT WORKS</div>
            <ol className="font-sans text-sm text-white/70 space-y-1.5 list-decimal list-inside">
              <li>Staff scans the visitor badge</li>
              <li>Confirm the extracted name</li>
              <li>Hit START to play</li>
              <li>Top scores immortalised</li>
            </ol>
          </div>
        </aside>
      </div>

      <footer className="mt-auto pt-6 font-arcade text-[10px] text-white/30">
        FLAPPYBRAIN © {new Date().getFullYear()} — v1
      </footer>
    </main>
  )
}
