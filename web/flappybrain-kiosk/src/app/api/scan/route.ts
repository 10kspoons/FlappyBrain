import { NextRequest, NextResponse } from 'next/server'
import { readNameFromBadge } from '@/lib/vision'

export const dynamic = 'force-dynamic'
export const runtime = 'nodejs'

export async function POST(request: NextRequest) {
  try {
    const formData = await request.formData()
    const file = formData.get('image')

    if (!file || typeof file === 'string') {
      return NextResponse.json({ error: 'No image provided' }, { status: 400 })
    }

    const blob = file as Blob
    const arrayBuffer = await blob.arrayBuffer()
    const base64 = Buffer.from(arrayBuffer).toString('base64')
    const mimeType = blob.type || 'image/jpeg'

    const name = await readNameFromBadge(base64, mimeType)
    return NextResponse.json({ name })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Scan failed'
    console.error('Scan error:', error)
    return NextResponse.json({ error: message }, { status: 500 })
  }
}
