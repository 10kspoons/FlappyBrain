import { NextRequest, NextResponse } from 'next/server'
import { getMostRecentScore, getTopScores, insertScore } from '@/lib/db'

export const dynamic = 'force-dynamic'
export const runtime = 'nodejs'

export async function GET() {
  try {
    const top10 = getTopScores(10)
    const recent = getMostRecentScore()
    return NextResponse.json({ top10, recent })
  } catch {
    return NextResponse.json({ error: 'DB unavailable' }, { status: 503 })
  }
}

export async function POST(request: NextRequest) {
  let body: unknown
  try {
    body = await request.json()
  } catch {
    return NextResponse.json({ error: 'Invalid JSON' }, { status: 400 })
  }

  const b = body as { playerName?: unknown; score?: unknown; sectionsCompleted?: unknown }
  const playerName = typeof b?.playerName === 'string' ? b.playerName.trim() : ''
  const score = Number.isFinite(b?.score) ? Math.max(0, Math.floor(b.score as number)) : 0
  const sectionsCompleted = Number.isFinite(b?.sectionsCompleted)
    ? Math.max(0, Math.floor(b.sectionsCompleted as number))
    : 0

  if (!playerName) {
    return NextResponse.json({ error: 'playerName is required' }, { status: 400 })
  }

  try {
    const inserted = insertScore(playerName, score, sectionsCompleted)
    return NextResponse.json(inserted)
  } catch {
    return NextResponse.json({ error: 'DB unavailable' }, { status: 503 })
  }
}
