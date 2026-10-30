import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, readFile, readdir, rm, rmdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { encryptBackup, verifyDownload } from './encryption.mjs';

async function fixture(run) {
  const directory = await mkdtemp(join(tmpdir(), 'recipe-encryption-test-'));
  try {
    await run(directory);
  } finally {
    for (const name of await readdir(directory)) {
      await rm(join(directory, name), { recursive: true, force: true });
    }
    await rmdir(directory);
  }
}

function command(tool, args) {
  const result = spawnSync(tool, args, { encoding: 'utf8' });
  assert.equal(result.status, 0, `${tool} failed: ${result.error?.code ?? result.stderr}`);
  return result;
}

async function keys(directory) {
  const identity = join(directory, 'identity.txt');
  command('age-keygen', ['-o', identity]);
  return { identity, recipient: command('age-keygen', ['-y', identity]).stdout.trim() };
}

test('real age roundtrip publishes only ciphertext, removes plaintext, and verifies downloaded bytes', async () => {
  await fixture(async (directory) => {
    const { identity, recipient } = await keys(directory);
    const plaintext = Buffer.from('Synthetic backup archive bytes, not real data.');
    const result = await encryptBackup({
      recipient, root: directory, maxBytes: 1024 * 1024,
      exportArchive: (path) => writeFile(path, plaintext, { flag: 'wx' }),
    });
    assert.deepEqual(await readdir(result.directory), ['recovery.zip.age']);
    assert.equal((await readFile(result.path, 'utf8')).startsWith('age-encryption.org/v1\n'), true);
    const recovered = join(directory, 'recovered.zip');
    command('age', ['--decrypt', '-i', identity, '-o', recovered, result.path]);
    assert.deepEqual(await readFile(recovered), plaintext);
    await verifyDownload(result.directory, result.sha256, result.bytes);
    assert.equal((await readdir(directory)).some((name) => name.startsWith('recipe-plaintext-')), false);
    const changed = await readFile(result.path);
    changed[changed.length - 1] ^= 1;
    await writeFile(result.path, changed);
    await assert.rejects(verifyDownload(result.directory, result.sha256, result.bytes), /SHA-256 mismatch/);
    await writeFile(result.path, 'tampered');
    await assert.rejects(verifyDownload(result.directory, result.sha256, result.bytes), /verification/);
  });
});

test('a recipient with an invalid checksum fails before source export', async () => {
  await fixture(async (directory) => {
    const { recipient } = await keys(directory);
    const invalid = recipient.slice(0, -1) + (recipient.endsWith('q') ? 'p' : 'q');
    let exported = false;
    await assert.rejects(encryptBackup({
      recipient: invalid, root: directory, maxBytes: 1024,
      exportArchive: async () => { exported = true; },
    }));
    assert.equal(exported, false);
  });
});

test('encrypted size cap accepts its exact boundary and rejects one byte less', async () => {
  await fixture(async (directory) => {
    const { recipient } = await keys(directory);
    const options = {
      recipient, root: directory,
      exportArchive: (path) => writeFile(path, 'fixed-size synthetic bytes', { flag: 'wx' }),
    };
    const initial = await encryptBackup({ ...options, maxBytes: 1024 * 1024 });
    const boundary = await encryptBackup({ ...options, maxBytes: initial.bytes });
    assert.equal(boundary.bytes, initial.bytes);
    await assert.rejects(encryptBackup({ ...options, maxBytes: initial.bytes - 1 }), /size limit/);
  });
});

test('cancellation after export cleans plaintext and publishes nothing', async () => {
  await fixture(async (directory) => {
    const { recipient } = await keys(directory);
    const before = (await readdir(directory)).sort();
    const controller = new AbortController();
    await assert.rejects(encryptBackup({
      recipient, root: directory, maxBytes: 1024, signal: controller.signal,
      exportArchive: async (path) => {
        await writeFile(path, 'synthetic data');
        controller.abort();
      },
    }));
    assert.deepEqual((await readdir(directory)).sort(), before);
  });
});

test('invalid or missing recipient fails before export', async () => {
  for (const recipient of ['', 'AGE-SECRET-KEY-DO-NOT-ACCEPT', 'age1invalid']) {
    await fixture(async (directory) => {
      let exported = false;
      await assert.rejects(encryptBackup({
        recipient, root: directory, maxBytes: 1024,
        exportArchive: async () => { exported = true; },
      }), /recipient/);
      assert.equal(exported, false);
      assert.deepEqual(await readdir(directory), []);
    });
  }
});

test('export, encryption, and size-limit failures clean staging and publish nothing', async () => {
  for (const failure of ['export', 'encrypt', 'limit']) {
    await fixture(async (directory) => {
      const { recipient } = await keys(directory);
      const before = (await readdir(directory)).sort();
      await assert.rejects(encryptBackup({
        recipient, root: directory, maxBytes: failure === 'limit' ? 1 : 1024 * 1024,
        ageCommand: failure === 'encrypt' ? 'missing-age-executable-for-test' : 'age',
        exportArchive: async (path) => {
          await writeFile(path, 'synthetic data');
          if (failure === 'export') throw new Error('synthetic export failure');
        },
      }));
      assert.deepEqual((await readdir(directory)).sort(), before);
    });
  }
});

test('download verification rejects extra files and missing metadata', async () => {
  await fixture(async (directory) => {
    await writeFile(join(directory, 'recovery.zip.age'), 'not ciphertext');
    await assert.rejects(verifyDownload(directory, '', 0), /verification/);
    await writeFile(join(directory, 'plaintext.zip'), 'must never be uploaded');
    await assert.rejects(verifyDownload(directory, 'a'.repeat(64), 14), /verification/);
  });
});
