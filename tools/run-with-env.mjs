import { spawn } from 'node:child_process';

const required = [
  'DATABASE_URL', 'DATABASE_URL_UNPOOLED', 'NEON_BRANCH',
  'AWS_ENDPOINT_URL_S3', 'AWS_REGION', 'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY',
];
const missing = required.filter((key) => !process.env[key]?.trim());
if (missing.length > 0) {
  throw new Error(`Missing Neon configuration: ${missing.join(', ')}. Run neon deploy or neon env pull.`);
}

const [command, ...args] = process.argv.slice(2);
if (!command) {
  throw new Error('Provide a command to run with the loaded environment.');
}

const child = spawn(command, args, { stdio: 'inherit', env: process.env });
child.on('error', (error) => {
  console.error(`Could not start ${command}: ${error.message}`);
  process.exitCode = 1;
});
child.on('exit', (code) => {
  process.exitCode = code ?? 1;
});
