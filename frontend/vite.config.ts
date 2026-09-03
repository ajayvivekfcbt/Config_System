import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    host: true, // listen on 0.0.0.0 so VS Code port forwarding can reach it
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
        // rewrite Origin so the backend CORS policy sees a known localhost origin
        // regardless of which URL the browser used to reach the forwarded port
        headers: { Origin: "http://localhost:5173" },
      },
    },
  },
});
