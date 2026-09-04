import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  base: './',
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../Runiq.AI.Core/Studio/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('/node_modules/react') || id.includes('/node_modules/react-dom')) return 'react';
          if (id.includes('/node_modules/@xyflow')) return 'flow';
          if (id.includes('/node_modules/lucide-react')) return 'icons';
          return undefined;
        },
      },
    },
  },
});
