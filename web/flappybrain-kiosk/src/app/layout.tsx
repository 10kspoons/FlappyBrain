import type { Metadata, Viewport } from 'next'
import './globals.css'
import ServiceWorkerRegistration from '@/components/ServiceWorkerRegistration'

export const metadata: Metadata = {
  title: 'FlappyBrain Kiosk',
  description: 'BCI-adapted Flappy Bird arcade kiosk',
  manifest: '/manifest.json',
  applicationName: 'FlappyBrain',
  appleWebApp: {
    capable: true,
    title: 'FlappyBrain',
    statusBarStyle: 'black-translucent',
  },
}

export const viewport: Viewport = {
  themeColor: '#00ffe5',
  width: 'device-width',
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=Press+Start+2P&display=swap"
          rel="stylesheet"
        />
        <link rel="icon" href="/icon-192.png" type="image/png" />
      </head>
      <body className="min-h-screen">
        <ServiceWorkerRegistration />
        {children}
      </body>
    </html>
  )
}
