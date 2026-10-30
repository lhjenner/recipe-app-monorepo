import assert from 'node:assert/strict';
import test from 'node:test';
import { validateConfiguration, validateRun } from './encrypted-backup.mjs';

const configuration = {
  DATABASE_URL_UNPOOLED: 'synthetic', AWS_ENDPOINT_URL_S3: 'synthetic',
  AWS_REGION: 'ap-southeast-1', AWS_ACCESS_KEY_ID: 'synthetic',
  AWS_SECRET_ACCESS_KEY: 'synthetic', BACKUP_BUCKET: 'recipe-images-dev',
  BACKUP_AGE_RECIPIENT: 'age1ql3z7hjy54pw3hyww5ayyfg7zqgvc7w3j2elw8zmrj2kg5sfn9aqmcac8p',
  BACKUP_DEFAULT_BRANCH: 'main', GITHUB_REF: 'refs/heads/main',
  GITHUB_EVENT_NAME: 'workflow_dispatch',
  BACKUP_IMAGE_CONSISTENCY_CONFIRMED: 'true',
};

test('configured development backup passes preflight without a private recovery key', () => {
  validateConfiguration(configuration);
});

test('missing configuration, unconfirmed consistency, wrong bucket or branch fails closed', () => {
  for (const [key, value] of [
    ['DATABASE_URL_UNPOOLED', ''], ['AWS_SECRET_ACCESS_KEY', ''],
    ['BACKUP_AGE_RECIPIENT', ''], ['BACKUP_IMAGE_CONSISTENCY_CONFIRMED', ''],
    ['BACKUP_BUCKET', 'production-images'], ['GITHUB_REF', 'refs/heads/untrusted'],
  ]) {
    assert.throws(() => validateConfiguration({ ...configuration, [key]: value }));
  }
});

test('trusted branch requires an explicit manual-run opt-in', () => {
  const branch = { ...configuration, GITHUB_REF: 'refs/heads/fix/backup', GITHUB_EVENT_NAME: 'workflow_dispatch' };
  assert.throws(() => validateRun(branch));
  assert.equal(validateRun({ ...branch, BACKUP_TEST_BRANCH: 'true' }), 'test');
  validateConfiguration({ ...branch, BACKUP_TEST_BRANCH: 'true' });
  assert.equal(validateRun(configuration), 'dev');
  assert.equal(validateRun({ ...configuration, GITHUB_EVENT_NAME: 'workflow_dispatch', BACKUP_TEST_BRANCH: 'true' }), 'test');
});

test('test opt-in never permits PR events or tag refs', () => {
  for (const [event, ref] of [
    ['pull_request', 'refs/pull/64/merge'],
    ['pull_request_target', 'refs/heads/main'],
    ['workflow_dispatch', 'refs/tags/test'],
    ['schedule', 'refs/heads/fix/backup'],
  ]) {
    assert.throws(() => validateRun({
      ...configuration, GITHUB_EVENT_NAME: event, GITHUB_REF: ref, BACKUP_TEST_BRANCH: 'true',
    }));
  }
});
