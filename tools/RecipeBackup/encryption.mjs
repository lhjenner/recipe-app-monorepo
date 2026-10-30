import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { chmod, mkdtemp, open, readdir, rename, rm, stat } from 'node:fs/promises';
import { join } from 'node:path';

export function validateRecipient(recipient) {
  if (typeof recipient !== 'string' || !/^age1[023456789acdefghjklmnpqrstuvwxyz]{58}$/.test(recipient)) {
    throw new Error('Set BACKUP_AGE_RECIPIENT to one valid public age1 recipient, never a private identity.');
  }
}

export async function runCommand(command, args, {
  signal, probe = false, phase = 'Backup command', safeDiagnostics = false,
} = {}) {
  await new Promise((resolve, reject) => {
    let startError = false;
    const child = spawn(command, args, {
      shell: false, signal,
      stdio: [probe ? 'pipe' : 'ignore', probe ? 'ignore' : 'inherit', safeDiagnostics ? 'inherit' : 'ignore'],
    });
    if (probe) {
      child.stdin.on('error', () => { /* An early command failure is reported by close/error below. */ });
      child.stdin.end();
    }
    child.once('error', () => { startError = true; });
    child.once('close', (code) => {
      if (startError) reject(new Error(`${phase} could not start or was cancelled.`));
      else if (code === 0) resolve();
      else reject(new Error(`${phase} failed (exit ${code ?? 'signal'}); no success claimed.`));
    });
  });
}

async function checksum(path) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(path)) hash.update(chunk);
  return hash.digest('hex');
}

export async function encryptBackup({
  recipient, root, exportArchive, maxBytes, ageCommand = 'age', signal,
}) {
  validateRecipient(recipient);
  if (!Number.isSafeInteger(maxBytes) || maxBytes <= 0) throw new Error('A positive encrypted size limit is required.');
  // Validate the actual recipient/checksum and executable before reading source data.
  console.log('Starting age recipient validation.');
  await runCommand(ageCommand, ['-r', recipient], { signal, probe: true, phase: 'Age recipient validation' });
  let plaintextDirectory;
  let directory;
  let completed = false;
  try {
    plaintextDirectory = await mkdtemp(join(root, 'recipe-plaintext-'));
    await chmod(plaintextDirectory, 0o700);
    directory = await mkdtemp(join(root, 'recipe-encrypted-'));
    await chmod(directory, 0o700);
    const archive = join(plaintextDirectory, 'recovery.zip');
    const partial = join(directory, 'recovery.zip.age.partial');
    const path = join(directory, 'recovery.zip.age');
    console.log('Starting database and referenced-image export.');
    await exportArchive(archive);
    signal?.throwIfAborted();
    console.log('Starting archive encryption.');
    await runCommand(ageCommand, ['-r', recipient, '-o', partial, archive], { signal, phase: 'Archive encryption' });
    const bytes = (await stat(partial)).size;
    if (bytes <= 0 || bytes > maxBytes) throw new Error('Encrypted backup exceeds the configured upload size limit.');
    const sha256 = await checksum(partial);
    signal?.throwIfAborted();
    await rename(partial, path);
    completed = true;
    return { directory, path, sha256, bytes };
  } finally {
    // Both directories are unique children created and owned by this invocation.
    if (plaintextDirectory) await rm(plaintextDirectory, { recursive: true, force: true });
    if (!completed && directory) await rm(directory, { recursive: true, force: true });
  }
}

export async function verifyDownload(directory, expectedHash, expectedBytes) {
  const names = await readdir(directory);
  const path = join(directory, 'recovery.zip.age');
  if (!/^[a-f0-9]{64}$/.test(expectedHash) || !Number.isSafeInteger(expectedBytes) || expectedBytes <= 0
    || names.length !== 1 || names[0] !== 'recovery.zip.age') {
    throw new Error('Encrypted artifact verification failed: unexpected files or metadata.');
  }
  const file = await open(path, 'r');
  try {
    const expectedHeader = Buffer.from('age-encryption.org/v1\n');
    const header = Buffer.alloc(expectedHeader.length);
    await file.read(header, 0, header.length, 0);
    if (!header.equals(expectedHeader)
      || (await file.stat()).size !== expectedBytes || await checksum(path) !== expectedHash) {
      throw new Error('Encrypted artifact verification failed: header, length, or SHA-256 mismatch.');
    }
  } finally {
    await file.close();
  }
}
