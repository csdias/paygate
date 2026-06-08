import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/telemetry': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      // Backoffice talks to the API's payment endpoints (list pending, approve, reject).
      '/payments': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
