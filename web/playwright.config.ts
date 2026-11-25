import { defineConfig } from '@playwright/test';
import { resolve } from 'node:path';

process.env.KWA_E2E_DIR ??= resolve('.e2e', `run-${Date.now()}`);
process.env.KWA_E2E_PORT ??= '15871';
export default defineConfig({
  testDir: './e2e', testMatch: '**/*.spec.ts', workers: 1, fullyParallel: false,
  timeout: 30_000, expect: { timeout: 10_000 },
  use: { baseURL: `http://127.0.0.1:${process.env.KWA_E2E_PORT}`, headless: true, launchOptions: process.env.KWA_CHROMIUM ? { executablePath: process.env.KWA_CHROMIUM } : {} },
  webServer: {
    command: 'bun e2e/host.ts', url: `http://127.0.0.1:${process.env.KWA_E2E_PORT}/api/auth/status`,
    reuseExistingServer: false, timeout: 60_000,
    gracefulShutdown: { signal: 'SIGTERM', timeout: 5_000 },
  },
});
