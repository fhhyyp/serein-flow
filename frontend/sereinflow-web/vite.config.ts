import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// Vite configuration reference: https://vite.dev/config/
// Vite 配置参考：https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://127.0.0.1:5178',
        changeOrigin: true,
      },
    },
  },
})
