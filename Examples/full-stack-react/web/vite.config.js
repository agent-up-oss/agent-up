import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  define: {
    __API_PORT__: JSON.stringify(process.env.API_PORT || '5601'),
    __AGENT_UP_AUDIT_ENDPOINT__: JSON.stringify(process.env.AGENT_UP_AUDIT_ENDPOINT || ''),
    __AGENT_UP_WORKSPACE_ID__: JSON.stringify(process.env.AGENT_UP_WORKSPACE_ID || ''),
    __AGENT_UP_APPLICATION__: JSON.stringify(process.env.AGENT_UP_APPLICATION || 'Web'),
  },
  server: {
    host: '0.0.0.0',
    port: Number(process.env.WEB_PORT || 5600),
    strictPort: true,
  },
});
