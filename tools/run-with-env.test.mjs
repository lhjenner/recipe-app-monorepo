import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, rmdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const launcher = fileURLToPath(new URL('./run-with-env.mjs', import.meta.url));
const keys = [
  'DATABASE_URL', 'DATABASE_URL_UNPOOLED', 'NEON_BRANCH',
  'AWS_ENDPOINT_URL_S3', 'AWS_REGION', 'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY',
];
const cleanEnv = Object.fromEntries(Object.entries(process.env)
  .filter(([key]) => !keys.includes(key)));

test('environment-file values reach the child process', () => {
  const directory = mkdtempSync(join(tmpdir(), 'neon-launcher-test-'));
  try {
    const envFile = join(directory, 'synthetic.env');
    writeFileSync(envFile, keys.map((key) => `${key}=synthetic-test`).join('\n'));
    const result = spawnSync(process.execPath, [
      `--env-file=${envFile}`, launcher, process.execPath, '-e',
      `process.exit(${JSON.stringify(keys)}.every(key => process.env[key] === 'synthetic-test') ? 0 : 1)`,
    ], { env: cleanEnv, encoding: 'utf8' });

    assert.equal(result.status, 0, result.stderr);
  } finally {
    rmSync(join(directory, 'synthetic.env'));
    rmdirSync(directory);
  }
});

test('missing Neon configuration fails before executing the child', () => {
  const result = spawnSync(process.execPath, [
    launcher, process.execPath, '-e', 'console.log("CHILD_STARTED")',
  ], { env: cleanEnv, encoding: 'utf8' });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Missing Neon configuration/);
  assert.doesNotMatch(result.stdout, /CHILD_STARTED/);
});

test('a child failure propagates its exit code', () => {
  const env = { ...cleanEnv, ...Object.fromEntries(keys.map((key) => [key, 'synthetic-test'])) };
  const result = spawnSync(process.execPath, [
    launcher, process.execPath, '-e', 'process.exit(7)',
  ], { env, encoding: 'utf8' });

  assert.equal(result.status, 7, result.stderr);
});
