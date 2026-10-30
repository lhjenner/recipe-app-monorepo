import assert from 'node:assert/strict';
import test from 'node:test';
import { retainBackups } from './retention.mjs';

function fixture() {
  const deleted = [];
  const runs = new Map(Array.from({ length: 10 }, (_, index) => {
    const id = index + 1;
    return [id, {
      id, workflow_id: 55, path: '.github/workflows/backup.yml', run_attempt: 1,
      head_branch: 'main', event: 'schedule', status: 'completed', conclusion: 'success',
    }];
  }));
  runs.get(10).status = 'in_progress';
  runs.get(10).conclusion = null;
  const artifacts = Array.from(runs.keys(), (id) => ({
    id, name: `recipe-backup-dev-${id}-1`, expired: false, size_in_bytes: 100,
    created_at: new Date(Date.UTC(2026, 9, id)).toISOString(), workflow_run: { id },
  }));
  const github = {
    paginate: async () => artifacts,
    rest: { actions: {
      listArtifactsForRepo() {},
      getArtifact: async ({ artifact_id }) => ({ data: artifacts.find((item) => item.id === artifact_id) }),
      getWorkflowRun: async ({ run_id }) => ({ data: runs.get(run_id) }),
      deleteArtifact: async ({ artifact_id }) => { deleted.push(artifact_id); },
    } },
  };
  const options = {
    github, context: { repo: { owner: 'test', repo: 'test' }, runId: 10 },
    artifactId: 10, artifactName: 'recipe-backup-dev-10-1',
    core: { info() {} },
  };
  return { deleted, runs, artifacts, options };
}

test('retains current verified upload plus five newest successful backups', async () => {
  const { options, deleted } = fixture();
  await retainBackups(options);
  assert.deepEqual(deleted.sort((a, b) => a - b), [1, 2, 3, 4]);
});

test('failed uploads do not displace successful backups; unrelated and active runs are untouched', async () => {
  const { options, deleted, artifacts, runs } = fixture();
  runs.get(9).conclusion = 'failure';
  runs.get(8).conclusion = 'cancelled';
  runs.get(7).status = 'in_progress';
  runs.get(6).workflow_id = 99;
  artifacts[4].name = 'backend-trx';
  await retainBackups(options);
  assert.deepEqual(deleted.sort((a, b) => a - b), [8, 9]);
});

test('no deletion when replacement metadata is missing, expired, wrong, or not current', async () => {
  for (const mutate of [
    (f) => { f.options.artifactId = 999; },
    (f) => { f.artifacts[9].expired = true; },
    (f) => { f.artifacts[9].name = 'wrong'; },
    (f) => { f.artifacts[9].workflow_run.id = 9; },
    (f) => { f.runs.get(10).path = '.github/workflows/unrelated.yml'; },
    (f) => { f.artifacts[9].size_in_bytes = 0; },
  ]) {
    const f = fixture();
    mutate(f);
    await assert.rejects(retainBackups(f.options), /replacement|workflow/);
    assert.deepEqual(f.deleted, []);
  }
});

test('list or run lookup failure prevents all deletions', async () => {
  for (const failure of ['list', 'run']) {
    const { options, deleted } = fixture();
    if (failure === 'list') options.github.paginate = async () => { throw new Error('API unavailable'); };
    else {
      const original = options.github.rest.actions.getWorkflowRun;
      options.github.rest.actions.getWorkflowRun = async (args) => {
        if (args.run_id === 3) throw new Error('API unavailable');
        return original(args);
      };
    }
    await assert.rejects(retainBackups(options), /API unavailable/);
    assert.deepEqual(deleted, []);
  }
});

test('rerun artifacts from earlier unverified attempts never count as successful backups', async () => {
  const { options, artifacts, runs, deleted } = fixture();
  runs.get(9).run_attempt = 2;
  artifacts.push({
    ...artifacts[8], id: 99, name: 'recipe-backup-dev-9-2',
  });
  artifacts.push({
    ...artifacts[9], id: 100, name: 'recipe-backup-dev-10-0',
  });
  await retainBackups(options);
  assert.deepEqual(deleted.sort((a, b) => a - b), [1, 2, 3, 4, 9, 100]);
});

test('rerunning only failed verification jobs can reuse the latest uploaded artifact', async () => {
  const { options, runs, deleted } = fixture();
  runs.get(9).run_attempt = 3;
  await retainBackups(options);
  assert.deepEqual(deleted.sort((a, b) => a - b), [1, 2, 3, 4]);
});

test('retention errors propagate instead of claiming successful cleanup', async () => {
  const { options } = fixture();
  options.github.rest.actions.deleteArtifact = async () => { throw new Error('forbidden'); };
  await assert.rejects(retainBackups(options), /forbidden/);
});
