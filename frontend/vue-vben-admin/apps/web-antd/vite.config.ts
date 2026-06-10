import { defineConfig } from '@vben/vite-config';

export default defineConfig(async () => {
  return {
    application: {},
    vite: {
      server: {
        allowedHosts: ['pvqrregz2.neiwangyun.net'],
        proxy: {
          '/api': {
            changeOrigin: true,
            rewrite: (path) => path.replace(/^\/api/, ''),
            target: 'http://localhost:5021',
            ws: true,
          },
        },
      },
    },
  };
});
