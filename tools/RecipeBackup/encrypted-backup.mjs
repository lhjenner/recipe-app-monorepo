import { appendFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import { encryptBackup, runCommand, validateRecipient, verifyDownload } from './encryption.mjs';

export function validateConfiguration(env) {
  const required = [
    'DATABASE_URL_UNPOOLED', 'AWS_ENDPOINT_URL_S3', 'AWS_REGION',
    'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY', 'BACKUP_BUCKET', 'BACKUP_AGE_RECIPIENT',
  ];
  const missing = required.filter((key) => !env[key]?.trim());
  if (missing.length) throw new Error(`Missing backup configuration: ${missing.join(', ')}`);
  validateRecipient(env.BACKUP_AGE_RECIPIENT);
  if (env.BACKUP_BUCKET !== 'recipe-images-dev') throw new Error('This workflow is restricted to recipe-images-dev.');
  if (env.GITHUB_REF !== `refs/heads/${env.BACKUP_DEFAULT_BRANCH}`) {
    throw new Error('Run this credential-bearing workflow only from the default branch.');
  }
  if (env.BACKUP_IMAGE_CONSISTENCY_CONFIRMED !== 'true') {
    throw new Error('Confirm immutable/retained image keys or a no-image-write backup window before enabling backups.');
  }
}

async function main() {
  const [operation, first, second] = process.argv.slice(2);
  if (operation === 'check-config') {
    validateConfiguration(process.env);
    console.log('PASS: required backup settings are present; no credentials printed.');
    return;
  }
  if (operation === 'verify-download' && first) {
    await verifyDownload(first, process.env.BACKUP_SHA256, Number(process.env.BACKUP_BYTES));
    console.log('PASS: downloaded encrypted artifact has the exact expected bytes and SHA-256.');
    return;
  }
  if (operation !== 'export-encrypted' || !first || !second) {
    throw new Error('Usage: encrypted-backup.mjs check-config | export-encrypted <dll> <temp-root> | verify-download <directory>');
  }
  validateConfiguration(process.env);
  const controller = new AbortController();
  const cancel = () => controller.abort();
  process.once('SIGINT', cancel);
  process.once('SIGTERM', cancel);
  try {
    const result = await encryptBackup({
      recipient: process.env.BACKUP_AGE_RECIPIENT, root: second,
      maxBytes: 60 * 1024 * 1024, signal: controller.signal,
      exportArchive: (path) => runCommand('dotnet', [first, 'export', path], { signal: controller.signal }),
    });
    await verifyDownload(result.directory, result.sha256, result.bytes);
    if (!process.env.GITHUB_OUTPUT) throw new Error('GITHUB_OUTPUT is required before publishing upload metadata.');
    await appendFile(process.env.GITHUB_OUTPUT,
      `path=${result.path}\nsha256=${result.sha256}\nbytes=${result.bytes}\n`);
    console.log(`PASS: encrypted backup ready; bytes=${result.bytes}; sixCopiesBytes=${result.bytes * 6}; sevenCopiesBytes=${result.bytes * 7}.`);
    if (process.env.GITHUB_STEP_SUMMARY) {
      await appendFile(process.env.GITHUB_STEP_SUMMARY,
        `### Encrypted development backup\n- Encrypted bytes: ${result.bytes}\n- SHA-256: \`${result.sha256}\`\n`
        + `- Six copies: ${result.bytes * 6} bytes; temporary seven: ${result.bytes * 7} bytes.\n`
        + '- Upload/re-download verification must succeed before retention cleanup.\n');
    }
  } finally {
    process.removeListener('SIGINT', cancel);
    process.removeListener('SIGTERM', cancel);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(`FAILED: ${error.message}`);
    process.exitCode = 1;
  });
}
