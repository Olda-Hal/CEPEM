import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    // Proxy API calls to the HealthcareAPI in dev
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true
      },
      // Service health probes (mirrors nginx.conf in the production image)
      '/health': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true
      },
      '/db-health': {
        target: 'http://127.0.0.1:5001',
        changeOrigin: true,
        rewrite: () => '/api/health'
      }
    }
  },
  build: {
    outDir: 'dist',
    sourcemap: false
  },
  test: {
    environment: 'jsdom',
    exclude: ['**/node_modules/**', '**/e2e/**'],
    globals: true,
    setupFiles: './src/setupTests.ts',
    environmentOptions: {
      jsdom: {
        // A real origin so localStorage is available
        url: 'http://localhost:3000'
      }
    }
  }
} as Parameters<typeof defineConfig>[0]);
