import type { Config } from 'tailwindcss'

const config: Config = {
  content: ['./src/**/*.{js,ts,jsx,tsx,mdx}'],
  theme: {
    extend: {
      colors: {
        bg: '#0a0a0f',
        cyan: { neon: '#00ffe5' },
        amber: { hot: '#ffb800' },
        red: { neon: '#ff2d55' },
      },
      fontFamily: {
        arcade: ['"Press Start 2P"', 'monospace'],
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        'neon-cyan': '0 0 10px #00ffe5, 0 0 20px #00ffe5, 0 0 40px #00ffe5',
        'neon-amber': '0 0 10px #ffb800, 0 0 20px #ffb800',
        'neon-red': '0 0 10px #ff2d55, 0 0 20px #ff2d55',
        'neon-gold': '0 0 10px #ffd700, 0 0 20px #ffd700',
        'neon-silver': '0 0 10px #c0c0c0, 0 0 20px #c0c0c0',
        'neon-bronze': '0 0 10px #cd7f32, 0 0 20px #cd7f32',
      },
      animation: {
        'pulse-neon': 'pulse-neon 1.5s ease-in-out infinite',
        'flash': 'flash 0.8s ease-in-out infinite',
      },
      keyframes: {
        'pulse-neon': {
          '0%, 100%': { boxShadow: '0 0 10px #00ffe5, 0 0 20px #00ffe5', opacity: '1' },
          '50%': { boxShadow: '0 0 20px #00ffe5, 0 0 40px #00ffe5, 0 0 60px #00ffe5', opacity: '0.95' },
        },
        'flash': {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.4' },
        },
      },
    },
  },
  plugins: [],
}

export default config
