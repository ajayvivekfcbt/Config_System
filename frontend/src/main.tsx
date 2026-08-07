import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import "./styles.css";

// Attach the shared app key to every API request so the backend accepts calls
// only from this application (direct curl/Postman/Swagger calls are rejected).
const APP_KEY = (import.meta.env.VITE_APP_KEY as string) || "config-system-web-app";
const originalFetch = window.fetch.bind(window);
window.fetch = (input: RequestInfo | URL, init: RequestInit = {}) => {
  const headers = new Headers(init.headers ?? {});
  headers.set("X-App-Key", APP_KEY);
  return originalFetch(input, { ...init, headers });
};

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </React.StrictMode>
);
