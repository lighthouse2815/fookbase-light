import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': { target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5000', changeOrigin: true },
      '/hubs': { target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5000', changeOrigin: true, ws: true },
    },
  },
})
