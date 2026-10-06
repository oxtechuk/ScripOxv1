import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { resolve } from 'path';

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
  ],
  build: {
    outDir: 'dist',
    rollupOptions: {
      input: {
        popup:   resolve(__dirname, 'popup.html'),
        options: resolve(__dirname, 'options.html'),
      }
    }
  },
  resolve: {
    alias: { '@': resolve(__dirname, 'src') }
  }
});
