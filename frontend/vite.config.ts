import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

// The dev server proxies /api and /health to the ASP.NET Core backend so the
// browser talks to a single origin (avoids CORS and cookie complications in dev).
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5125',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5125',
        changeOrigin: true,
        ws: true,
      },
      '/health': {
        target: 'http://localhost:5125',
        changeOrigin: true,
      },
    },
  },
});
