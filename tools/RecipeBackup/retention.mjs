const workflowPath = '.github/workflows/backup.yml';
const backupName = /^recipe-backup-dev-\d+-\d+$/;

export async function retainBackups({ github, context, core, artifactId, artifactName }) {
  const repo = context.repo;
  const currentRun = (await github.rest.actions.getWorkflowRun({ ...repo, run_id: context.runId })).data;
  if (currentRun.path !== workflowPath || !['schedule', 'workflow_dispatch'].includes(currentRun.event)) {
    throw new Error('Unexpected backup workflow; no retention changes made.');
  }
  const replacement = (await github.rest.actions.getArtifact({ ...repo, artifact_id: artifactId })).data;
  if (!replacement || replacement.expired !== false || replacement.size_in_bytes <= 0
    || replacement.name !== artifactName || !backupName.test(replacement.name)
    || replacement.workflow_run?.id !== context.runId) {
    throw new Error('Verified replacement artifact is missing or invalid; no retention changes made.');
  }
  const artifacts = await github.paginate(github.rest.actions.listArtifactsForRepo, { ...repo, per_page: 100 });
  if (!artifacts.some((artifact) => artifact.id === replacement.id)) {
    throw new Error('Verified replacement is absent from the artifact listing; no retention changes made.');
  }
  const candidates = [];
  const failed = [];
  const runs = new Map([[context.runId, currentRun]]);
  for (const artifact of artifacts) {
    if (!backupName.test(artifact.name) || artifact.expired || !artifact.workflow_run?.id) continue;
    const runId = artifact.workflow_run.id;
    if (!runs.has(runId)) {
      runs.set(runId, (await github.rest.actions.getWorkflowRun({ ...repo, run_id: runId })).data);
    }
    const run = runs.get(runId);
    if (run.workflow_id !== currentRun.workflow_id || run.path !== workflowPath
      || run.head_branch !== currentRun.head_branch || !['schedule', 'workflow_dispatch'].includes(run.event)) continue;
    if (!artifact.name.startsWith(`recipe-backup-dev-${runId}-`)) continue;
    if (!Number.isFinite(Date.parse(artifact.created_at))) throw new Error('Invalid artifact timestamp; no retention changes made.');
    if (artifact.id === artifactId || (run.status === 'completed' && run.conclusion === 'success')) {
      candidates.push(artifact);
    } else if (run.status === 'completed' || runId === context.runId) {
      failed.push(artifact);
    }
  }
  // Treat this re-downloaded, verified upload as the replacement even while this run is in progress.
  // A "rerun failed jobs" may verify an upload from an earlier attempt.
  // Pick the newest uploaded attempt of each successful run, not the run's attempt number.
  const latest = new Map();
  const superseded = [];
  for (const artifact of candidates.filter((item) => item.id !== artifactId)) {
    const previous = latest.get(artifact.workflow_run.id);
    const attempt = Number(artifact.name.split('-').at(-1));
    if (!Number.isSafeInteger(attempt) || attempt < 1) throw new Error('Invalid artifact attempt; no retention changes made.');
    if (!previous || attempt > Number(previous.name.split('-').at(-1))) {
      if (previous) superseded.push(previous);
      latest.set(artifact.workflow_run.id, artifact);
    } else {
      superseded.push(artifact);
    }
  }
  const previous = [...latest.values()]
    .sort((left, right) => Date.parse(right.created_at) - Date.parse(left.created_at) || right.id - left.id);
  const remove = [...previous.slice(5), ...superseded, ...failed];
  for (const artifact of remove) {
    await github.rest.actions.deleteArtifact({ ...repo, artifact_id: artifact.id });
    core.info(`Deleted old development backup artifact ${artifact.id}.`);
  }
  core.info(`Retention complete: kept verified replacement and ${Math.min(previous.length, 5)} prior successful backups.`);
}
