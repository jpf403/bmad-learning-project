import { existsSync, readFileSync } from 'node:fs'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const CERT_PATH = './.certs/localhost.pem'
const KEY_PATH = './.certs/localhost.key'

// Reuses the same trusted ASP.NET Core dev certificate the backend serves
// with (exported via `dotnet dev-certs https --export-path ./.certs/localhost.pem
// --format Pem --no-password`), so the frontend also runs over HTTPS without
// a separate self-signed-cert trust prompt. Both origins sharing a scheme
// avoids Chrome's schemeful-same-site policy dropping the SameSite=Strict
// refreshToken cookie on cross-origin requests from an HTTP page to an
// HTTPS API (AD-13, Story 1.6 Task 11).
//
// This file also doubles as the Vitest config, so this must stay lazy/optional:
// CI and any machine that hasn't exported the cert yet must still be able to
// run tests and `vite dev` (just without HTTPS) without this throwing.
const httpsConfig =
  existsSync(CERT_PATH) && existsSync(KEY_PATH)
    ? { key: readFileSync(KEY_PATH), cert: readFileSync(CERT_PATH) }
    : undefined

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    https: httpsConfig,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.js'],
  },
})
