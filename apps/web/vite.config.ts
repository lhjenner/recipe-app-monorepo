import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: mode === 'neon' ? 'https://localhost:7209' : 'http://localhost:5105',
        changeOrigin: true,
        secure: mode !== 'neon',
      },
    },
  },
  test: {
    environment: 'jsdom',
    include: ['tests/unit/**/*.test.ts', 'tests/unit/**/*.test.tsx'],
    setupFiles: './tests/unit/setup.ts',
  },
}))
