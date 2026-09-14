import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': `${import.meta.dirname}/src` },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        // บอก API ว่าเบราว์เซอร์เปิดอยู่ที่ 5173 ไม่ใช่ 5080
        // ไม่งั้น redirect_uri ของ Google OAuth จะชี้กลับมาที่ 5080 ซึ่งไม่มีหน้าเว็บตอน dev
        headers: { 'x-forwarded-host': 'localhost:5173', 'x-forwarded-proto': 'http' },
      },
    },
  },
})
