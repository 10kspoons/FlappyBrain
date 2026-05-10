'use client'

import dynamic from 'next/dynamic'

const BadgeScanner = dynamic(() => import('@/components/BadgeScanner'), { ssr: false })

export default function ScanPage() {
  return <BadgeScanner />
}
