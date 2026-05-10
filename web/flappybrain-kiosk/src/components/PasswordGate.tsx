'use client'

import { useEffect, useState, type FormEvent } from 'react'

const STORAGE_KEY = 'flappybrain_auth'
const PASSWORD = 'Dhf2026!'

export default function PasswordGate({ children }: { children: React.ReactNode }) {
  const [authed, setAuthed] = useState<boolean | null>(null)
  const [input, setInput] = useState('')
  const [error, setError] = useState(false)
  const [shake, setShake] = useState(false)

  useEffect(() => {
    setAuthed(typeof window !== 'undefined' && window.localStorage.getItem(STORAGE_KEY) === '1')
  }, [])

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (input === PASSWORD) {
      window.localStorage.setItem(STORAGE_KEY, '1')
      setError(false)
      setAuthed(true)
      return
    }
    setError(true)
    setShake(true)
    setInput('')
    window.setTimeout(() => setShake(false), 450)
  }

  if (authed === null) {
    return null
  }

  if (authed) {
    return <>{children}</>
  }

  return (
    <div className='fixed inset-0 z-[9999] flex items-center justify-center bg-bg px-6'>
      <div
        className={`w-full max-w-md border border-cyan-neon/40 bg-black/70 rounded-lg p-7 md:p-9 backdrop-blur-sm ${
          shake ? 'animate-[gate-shake_0.4s_ease-in-out]' : ''
        }`}
        style={{ boxShadow: '0 0 30px rgba(0,255,229,0.15)' }}
      >
        <div className='flex flex-col items-center gap-3 mb-6'>
          <div className='font-arcade text-2xl md:text-3xl text-cyan-neon tracking-widest'>
            FLAPPY<span className='text-amber-hot'>BRAIN</span>
          </div>
          <div className='font-arcade text-[10px] text-white/40 tracking-wider'>STAFF ACCESS REQUIRED</div>
        </div>

        <form onSubmit={onSubmit} className='flex flex-col gap-4'>
          <label className='font-arcade text-[10px] text-cyan-neon/80 tracking-wider'>
            PASSWORD
          </label>
          <input
            type='password'
            autoFocus
            value={input}
            onChange={(e) => {
              setInput(e.target.value)
              if (error) setError(false)
            }}
            className='w-full bg-black/60 border border-cyan-neon/40 rounded px-4 py-3 font-arcade text-base text-white tracking-widest focus:outline-none focus:border-cyan-neon focus:shadow-neon-cyan'
            placeholder='••••••••'
          />
          {error && (
            <div className='font-arcade text-xs text-red-neon tracking-wider text-center'>
              ⛔ ACCESS DENIED
            </div>
          )}
          <button
            type='submit'
            className='mt-2 w-full font-arcade text-sm tracking-widest bg-cyan-neon/15 hover:bg-cyan-neon/25 border border-cyan-neon rounded py-3 text-cyan-neon transition-all hover:shadow-neon-cyan'
          >
            ENTER ▶
          </button>
        </form>

        <div className='mt-6 font-arcade text-[9px] text-white/30 tracking-wider text-center'>
          BCI ARCADE — AUTHORISED STAFF ONLY
        </div>
      </div>

      <style jsx global>{`
        @keyframes gate-shake {
          0%, 100% { transform: translateX(0); }
          20% { transform: translateX(-8px); }
          40% { transform: translateX(8px); }
          60% { transform: translateX(-6px); }
          80% { transform: translateX(6px); }
        }
      `}</style>
    </div>
  )
}
