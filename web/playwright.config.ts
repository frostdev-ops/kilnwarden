import { defineConfig } from '@playwright/test';
import { resolve } from 'node:path';

process.env.KWA_E2E_DIR ??= resolve('.e2e', `run-${Date.now()}`);
process.env.KWA_E2E_PORT ??= '15871';
// the setup wizard's own server (wizard.spec.ts): a fresh one, HTTPS, its relay and CA addresses dead ends (nothing leaves this PC)
process.env.KWA_E2E_WIZARD_DIR ??= resolve('.e2e', `wizard-${Date.now()}`);
process.env.KWA_E2E_WIZARD_PORT ??= '15872';
export default defineConfig({
  testDir: './e2e', testMatch: '**/*.spec.ts', workers: 1, fullyParallel: false,
  timeout: 30_000, expect: { timeout: 10_000 },
  use: { baseURL: `http://127.0.0.1:${process.env.KWA_E2E_PORT}`, headless: true, launchOptions: process.env.KWA_CHROMIUM ? { executablePath: process.env.KWA_CHROMIUM } : {} },
  webServer: [{
    command: 'bun e2e/host.ts', url: `http://127.0.0.1:${process.env.KWA_E2E_PORT}/api/auth/status`,
    reuseExistingServer: false, timeout: 60_000,
    gracefulShutdown: { signal: 'SIGTERM', timeout: 5_000 },
  }, {
    command: 'bun e2e/host.ts', url: `https://127.0.0.1:${process.env.KWA_E2E_WIZARD_PORT}/api/auth/status`, ignoreHTTPSErrors: true,
    reuseExistingServer: false, timeout: 60_000,
    gracefulShutdown: { signal: 'SIGTERM', timeout: 5_000 },
    env: {
      ...(process.env as Record<string, string>),
      KWA_E2E_DIR: process.env.KWA_E2E_WIZARD_DIR!, KWA_E2E_PORT: process.env.KWA_E2E_WIZARD_PORT!,
      KWA_E2E_DEVHOST_ARGS: '--tls self-signed --relay-origin https://127.0.0.1:9 --acme-directory https://127.0.0.1:9/directory',
    },
  }],
});
