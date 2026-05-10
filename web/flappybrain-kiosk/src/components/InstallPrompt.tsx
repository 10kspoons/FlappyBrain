'use client'

import { useEffect, useState } from 'react'

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

type Platform = 'ios' | 'android' | 'other' | null

function detectPlatform(): Platform {
  if (typeof navigator === 'undefined') return null
  const ua = navigator.userAgent
  const isIOS = /iphone|ipad|ipod/i.test(ua)
  const isAndroid = /android/i.test(ua)
  if (isIOS) return 'ios'
  if (isAndroid) return 'android'
  return 'other'
}

function isStandalone(): boolean {
  if (typeof window === 'undefined') return false
  return (
    window.matchMedia('(display-mode: standalone)').matches ||
    ('standalone' in window.navigator && (window.navigator as { standalone?: boolean }).standalone === true)
  )
}

export default function InstallPrompt() {
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null)
  const [platform, setPlatform] = useState<Platform>(null)
  const [installed, setInstalled] = useState(false)
  const [dismissed, setDismissed] = useState(false)

  useEffect(() => {
    if (isStandalone()) { setInstalled(true); return }

    setPlatform(detectPlatform())

    const handler = (e: Event) => {
      e.preventDefault()
      setDeferredPrompt(e as BeforeInstallPromptEvent)
    }
    window.addEventListener('beforeinstallprompt', handler)
    window.addEventListener('appinstalled', () => setInstalled(true))
    return () => window.removeEventListener('beforeinstallprompt', handler)
  }, [])

  const handleInstall = async () => {
    if (!deferredPrompt) return
    await deferredPrompt.prompt()
    const { outcome } = await deferredPrompt.userChoice
    if (outcome === 'accepted') setInstalled(true)
    setDeferredPrompt(null)
  }

  if (installed || dismissed) return null

  const bannerStyle: React.CSSProperties = {
    position: 'fixed',
    bottom: '1.5rem',
    left: '50%',
    transform: 'translateX(-50%)',
    zIndex: 9000,
    background: '#0a0a0f',
    border: '2px solid #00ffe5',
    borderRadius: '0.75rem',
    padding: '0.85rem 1.25rem',
    boxShadow: '0 0 24px #00ffe560',
    display: 'flex',
    alignItems: 'center',
    gap: '1rem',
    maxWidth: '90vw',
    whiteSpace: 'nowrap',
  }

  const textStyle: React.CSSProperties = {
    fontFamily: 'monospace',
    fontSize: '0.8rem',
    color: '#00ffe5',
    lineHeight: 1.4,
  }

  const closeBtn: React.CSSProperties = {
    background: 'none',
    border: 'none',
    color: '#ffffff80',
    fontSize: '1.1rem',
    cursor: 'pointer',
    padding: '0 0.25rem',
    lineHeight: 1,
  }

  // Android: show install button only when browser offers the prompt
  if (platform === 'android' && deferredPrompt) {
    return (
      <div style={bannerStyle}>
        <button
          onClick={handleInstall}
          style={{ ...textStyle, background: '#00ffe5', color: '#0a0a0f', border: 'none', borderRadius: '0.4rem', padding: '0.5rem 1rem', cursor: 'pointer', fontFamily: "'Press Start 2P', monospace", fontSize: '0.55rem' }}
        >
          ⬇ INSTALL APP
        </button>
        <button style={closeBtn} onClick={() => setDismissed(true)}>✕</button>
      </div>
    )
  }

  // iOS: always show the instructions banner (no JS prompt available)
  if (platform === 'ios') {
    return (
      <div style={bannerStyle}>
        <span style={{ fontSize: '1.4rem' }}>📲</span>
        <span style={textStyle}>
          Tap <strong style={{ color: '#fff' }}>Share</strong> then{' '}
          <strong style={{ color: '#fff' }}>Add to Home Screen</strong>
        </span>
        <button style={closeBtn} onClick={() => setDismissed(true)}>✕</button>
      </div>
    )
  }

  // Desktop / other: show install button if prompt available
  if (deferredPrompt) {
    return (
      <div style={bannerStyle}>
        <button onClick={handleInstall} style={{ ...textStyle, background: '#00ffe5', color: '#0a0a0f', border: 'none', borderRadius: '0.4rem', padding: '0.5rem 1rem', cursor: 'pointer' }}>
          ⬇ Install App
        </button>
        <button style={closeBtn} onClick={() => setDismissed(true)}>✕</button>
      </div>
    )
  }

  return null
}
