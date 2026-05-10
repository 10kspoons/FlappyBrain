import { NextRequest, NextResponse } from 'next/server'
import { getMostRecentScore, getTopScores, insertScore } from '@/lib/db'

export const dynamic = 'force-dynamic'
export const runtime = 'nodejs'

export async function GET() {
  const top10 = getTopScores(10)
  const recent = getMostRecentScore()
  return NextResponse.json({ top10, recent })
}

export async function POST(request: NextRequest) {
  try {
    const body = await request.json()
    const playerName = typeof body?.playerName === 'string' ? body.playerName.trim() : ''
    const score = Number.isFinite(body?.score) ? Math.max(0, Math.floor(body.score)) : 0
    const sectionsCompleted = Number.isFinite(body?.sectionsCompleted)
      ? Math.max(0, Math.floor(body.sectionsCompleted))
      : 0

    if (!playerName) {
      return NextResponse.json({ error: 'playerName is required' }, { status: 400 })
    }

    const inserted = insertScore(playerName, score, sectionsCompleted)
    return NextResponse.json(inserted)
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to save score'
    return NextResponse.json({ error: message }, { status: 500 })
  }
}
