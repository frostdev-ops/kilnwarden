// Disposable, offline fixtures only. Playwright owns this process; it owns and stops these exact child processes.
import { spawn, type ChildProcess } from 'node:child_process';
import { mkdirSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
const dir = process.env.KWA_E2E_DIR!;
mkdirSync(dir, { recursive: true });
const children: ChildProcess[] = [];
let stopping = false;
async function stop(code = 0) {
  if (stopping) return;
  stopping = true;
  for (const c of children.reverse()) {
    if (c.exitCode !== null) continue;
    c.kill('SIGTERM');
    await Promise.race([new Promise(r => c.once('exit', r)), new Promise(r => setTimeout(r, 3000))]);
    if (c.exitCode === null) c.kill('SIGKILL');
  }
  process.exit(code);
}
for (const signal of ['SIGINT', 'SIGTERM'] as const) process.on(signal, () => void stop());
function start(command: string, args: string[]) {
  const c = spawn(command, args, { stdio: ['ignore', 'pipe', 'pipe'] });
  children.push(c);
  // Never forward the setup link to the runner log. The tests read this disposable host's log file.
  c.stdout?.on('data', () => {});
  c.stderr?.on('data', d => process.stderr.write(d));
  c.once('exit', code => { if (!stopping) { console.error(`${command} exited (${code})`); void stop(1); } });
  return c;
}
// KWA_PYTHON: the interpreter (Windows runners have python, not python3)
start(process.env.KWA_PYTHON ?? 'python3', ['-I', resolve('e2e/kap-mock.py'), 'serve', '--dir', dir, '--quiet']);
const endpoint = resolve(dir, 'kiln/data/kiln-admin/endpoint.json');
for (let i = 0; !existsSync(endpoint); i++) {
  if (i > 100) throw Error('mock KAP endpoint did not appear');
  await new Promise(r => setTimeout(r, 50));
}
// KWA_E2E_DEVHOST_ARGS: more dev host arguments (the setup wizard's host: TLS, a relay and a CA that never answer)
const extra = (process.env.KWA_E2E_DEVHOST_ARGS ?? '').split(' ').filter(Boolean);
start('dotnet', [resolve('../tools/devhost/bin/Debug/net10.0/Kilnwarden.DevHost.dll'), '--server', dir, '--port', process.env.KWA_E2E_PORT!, '--web', resolve('dist'), ...extra]);
