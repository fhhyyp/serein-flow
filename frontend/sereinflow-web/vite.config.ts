import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// Vite configuration reference: https://vite.dev/config/
// Vite 配置参考：https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://127.0.0.1:8188',
        changeOrigin: true,
      },
      // SignalR uses a WebSocket transport in browsers. Keep the development
      // proxy aligned with the API proxy so the native client does not try to
      // connect to Vite itself (and silently fall back without live events).
      // SignalR 使用 WebSocket，开发代理必须转发 /hubs，否则客户端会连到
      // Vite 自身并在没有实时事件的情况下静默降级。
      '/hubs': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://127.0.0.1:8188',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
