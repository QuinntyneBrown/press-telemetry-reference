import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://localhost:5063', changeOrigin: true },
      '/hubs': { target: 'http://localhost:5063', changeOrigin: true, ws: true },
    },
  },
  resolve: { dedupe: ['react', 'react-dom'] },
  optimizeDeps: {
    // Reached only through linked workspace-library source; declare so Vite
    // pre-bundles them up front instead of discovering them mid-session.
    include: ['@microsoft/signalr', '@tanstack/react-query', 'react-router'],
  },
});
