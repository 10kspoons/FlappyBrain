import { NextRequest, NextResponse } from 'next/server'
import { getCurrentPlayer, setCurrentPlayer } from '@/lib/db'

export const dynamic = 'force-dynamic'
export const runtime = 'nodejs'

export async function GET() {
  try {
    const cp = getCurrentPlayer()
    return NextResponse.json({ name: cp.name, scannedAt: cp.scanned_at })
  } catch {
    return NextResponse.json({ error: 'DB unavailable' }, { status: 503 })
  }
}

export async function PUT(request: NextRequest) {
  let body: unknown
  try {
    body = await request.json()
  } catch {
    return NextResponse.json({ error: 'Invalid JSON' }, { status: 400 })
  }

  const raw = (body as { name?: unknown })?.name
  const name = typeof raw === 'string' && raw.trim() ? raw.trim() : null

  try {
    const updated = setCurrentPlayer(name)
    return NextResponse.json({ name: updated.name, scannedAt: updated.scanned_at })
  } catch {
    return NextResponse.json({ error: 'DB unavailable' }, { status: 503 })
  }
}
