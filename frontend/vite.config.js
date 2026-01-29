import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

// Build output is configured to drop static assets into the backend's /app folder.
// Folder layout expected:
//   OEMAuditDemo/
//     AuditDemo.WebApi/
//     frontend/
export default defineConfig(() => {
  const outDir = path.resolve(__dirname, '../AuditDemo.WebApi/app')
  return {
    plugins: [vue()],
    base: '/app/',
    build: {
      outDir,
      emptyOutDir: true
    },
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        // Development proxy to your IIS Express backend.
        // If your backend uses another port, change target.
        '/api': {
          target: 'http://localhost:9356',
          changeOrigin: true
        }
      }
    }
  }
})
