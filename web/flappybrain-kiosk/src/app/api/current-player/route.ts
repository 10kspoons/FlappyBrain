import { NextRequest, NextResponse } from 'next/server'
import { getCurrentPlayer, setCurrentPlayer } from '@/lib/db'

export const dynamic = 'force-dynamic'
export const runtime = 'nodejs'

export async function GET() {
  const cp = getCurrentPlayer()
  return NextResponse.json({ name: cp.name, scannedAt: cp.scanned_at })
}

export async function PUT(request: NextRequest) {
  try {
    const body = await request.json()
    const raw = body?.name
    const name = typeof raw === 'string' && raw.trim() ? raw.trim() : null
    const updated = setCurrentPlayer(name)
    return NextResponse.json({ name: updated.name, scannedAt: updated.scanned_at })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to set current player'
    return NextResponse.json({ error: message }, { status: 500 })
  }
}
