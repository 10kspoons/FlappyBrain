'use client'

import { useEffect, useRef, useState } from 'react'
import { useRouter } from 'next/navigation'

type Phase = 'PREVIEW' | 'CAPTURED' | 'PROCESSING' | 'RESULT' | 'CONFIRMING' | 'ERROR'

export default function BadgeScanner() {
  const router = useRouter()
  const videoRef = useRef<HTMLVideoElement | null>(null)
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const [phase, setPhase] = useState<Phase>('PREVIEW')
  const [name, setName] = useState<string>('')
  const [error, setError] = useState<string>('')
  const [capturedUrl, setCapturedUrl] = useState<string>('')
  const [capturedBlob, setCapturedBlob] = useState<Blob | null>(null)

  useEffect(() => {
    let cancelled = false

    async function startCamera() {
      if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
        setError('Camera not available in this browser')
        setPhase('ERROR')
        return
      }
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
          audio: false,
        })
        if (cancelled) {
          stream.getTracks().forEach((t) => t.stop())
          return
        }
        streamRef.current = stream
        if (videoRef.current) {
          videoRef.current.srcObject = stream
          await videoRef.current.play().catch(() => {})
        }
      } catch (err) {
        const msg = err instanceof Error ? err.message : 'Camera permission denied'
        setError(msg)
        setPhase('ERROR')
      }
    }

    startCamera()

    return () => {
      cancelled = true
      streamRef.current?.getTracks().forEach((t) => t.stop())
      streamRef.current = null
    }
  }, [])

  useEffect(() => {
    return () => {
      if (capturedUrl) URL.revokeObjectURL(capturedUrl)
    }
  }, [capturedUrl])

  const captureFrame = () => {
    const video = videoRef.current
    const canvas = canvasRef.current
    if (!video || !canvas) return

    const w = video.videoWidth || 1280
    const h = video.videoHeight || 720
    canvas.width = w
    canvas.height = h
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.drawImage(video, 0, 0, w, h)

    canvas.toBlob(
      (blob) => {
        if (!blob) {
          setError('Failed to capture image')
          setPhase('ERROR')
          return
        }
        if (capturedUrl) URL.revokeObjectURL(capturedUrl)
        setCapturedBlob(blob)
        setCapturedUrl(URL.createObjectURL(blob))
        setPhase('CAPTURED')
      },
      'image/jpeg',
      0.9
    )
  }

  const sendToServer = async (blob: Blob) => {
    setPhase('PROCESSING')
    setError('')
    try {
      const fd = new FormData()
      fd.append('image', blob, 'badge.jpg')
      const res = await fetch('/api/scan', { method: 'POST', body: fd })
      if (!res.ok) {
        const j = await res.json().catch(() => ({}))
        throw new Error(j?.error || `Scan failed (${res.status})`)
      }
      const data = (await res.json()) as { name: string }
      setName(data.name || 'Unknown')
      setPhase('RESULT')
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Scan failed'
      setError(msg)
      setPhase('ERROR')
    }
  }

  const onCapture = () => {
    if (phase !== 'PREVIEW') return
    captureFrame()
  }

  const onProcess = () => {
    if (phase !== 'CAPTURED' || !capturedBlob) return
    sendToServer(capturedBlob)
  }

  const onRetry = () => {
    if (capturedUrl) URL.revokeObjectURL(capturedUrl)
    setCapturedUrl('')
    setCapturedBlob(null)
    setName('')
    setError('')
    setPhase('PREVIEW')
  }

  const onConfirm = async () => {
    if (!name) return
    setPhase('CONFIRMING')
    try {
      const res = await fetch('/api/current-player', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name }),
      })
      if (!res.ok) throw new Error(`Failed to set player (${res.status})`)
      router.push('/')
      router.refresh()
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to set player'
      setError(msg)
      setPhase('ERROR')
    }
  }

  return (
    <div className="min-h-screen w-full flex flex-col items-center justify-start p-4 md:p-6 gap-4">
      <header className="w-full flex items-center justify-between max-w-3xl">
        <button
          onClick={() => router.push('/')}
          className="font-arcade text-xs text-white/60 hover:text-cyan-neon transition-colors"
        >
          ← BACK
        </button>
        <h1 className="font-arcade text-base md:text-xl text-cyan-neon">SCAN BADGE</h1>
        <div className="w-12" />
      </header>

      <div className="relative w-full max-w-3xl aspect-video rounded-lg overflow-hidden border-2 border-cyan-neon/40 shadow-neon-cyan bg-black">
        {phase === 'CAPTURED' || phase === 'PROCESSING' || phase === 'RESULT' ? (
          capturedUrl && (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={capturedUrl} alt="captured badge" className="w-full h-full object-contain" />
          )
        ) : (
          <video
            ref={videoRef}
            playsInline
            muted
            className="w-full h-full object-cover"
          />
        )}
        <canvas ref={canvasRef} className="hidden" />

        <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
          <div className="w-2/3 h-2/3 border-2 border-dashed border-cyan-neon/60 rounded" />
        </div>

        {phase === 'PROCESSING' && (
          <div className="absolute inset-0 bg-black/70 flex flex-col items-center justify-center gap-3">
            <div className="w-12 h-12 border-4 border-cyan-neon border-t-transparent rounded-full animate-spin" />
            <span className="font-arcade text-xs text-cyan-neon">READING NAME…</span>
          </div>
        )}
      </div>

      <div className="w-full max-w-3xl flex flex-col gap-4">
        {phase === 'PREVIEW' && (
          <button
            onClick={onCapture}
            className="font-arcade text-xl text-bg bg-cyan-neon px-8 py-5 rounded shadow-neon-cyan hover:brightness-110 transition active:scale-95"
          >
            CAPTURE
          </button>
        )}

        {phase === 'CAPTURED' && (
          <div className="flex gap-3">
            <button
              onClick={onProcess}
              className="flex-1 font-arcade text-lg text-bg bg-cyan-neon px-6 py-4 rounded shadow-neon-cyan hover:brightness-110 transition active:scale-95"
            >
              READ NAME
            </button>
            <button
              onClick={onRetry}
              className="flex-1 font-arcade text-lg text-amber-hot border-2 border-amber-hot px-6 py-4 rounded hover:bg-amber-hot/10 transition active:scale-95"
            >
              RETRY
            </button>
          </div>
        )}

        {phase === 'RESULT' && (
          <div className="flex flex-col gap-3 items-center">
            <div className="font-arcade text-[10px] text-white/50">EXTRACTED NAME</div>
            <div className="font-arcade text-2xl md:text-3xl text-amber-hot text-center px-4 py-3 border-2 border-amber-hot/60 rounded shadow-neon-amber bg-black/50">
              {name}
            </div>
            <div className="flex w-full gap-3 mt-2">
              <button
                onClick={onConfirm}
                className="flex-1 font-arcade text-lg text-bg bg-cyan-neon px-6 py-4 rounded shadow-neon-cyan hover:brightness-110 transition active:scale-95"
              >
                CONFIRM
              </button>
              <button
                onClick={onRetry}
                className="flex-1 font-arcade text-lg text-amber-hot border-2 border-amber-hot px-6 py-4 rounded hover:bg-amber-hot/10 transition active:scale-95"
              >
                RETRY
              </button>
            </div>
          </div>
        )}

        {phase === 'CONFIRMING' && (
          <div className="font-arcade text-sm text-cyan-neon text-center py-4">QUEUEING PLAYER…</div>
        )}

        {phase === 'ERROR' && (
          <div className="flex flex-col gap-3">
            <div className="font-arcade text-sm text-red-neon border border-red-neon/60 px-4 py-3 rounded bg-red-neon/10">
              {error || 'Something went wrong'}
            </div>
            <button
              onClick={onRetry}
              className="font-arcade text-lg text-amber-hot border-2 border-amber-hot px-6 py-4 rounded hover:bg-amber-hot/10 transition"
            >
              RETRY
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
