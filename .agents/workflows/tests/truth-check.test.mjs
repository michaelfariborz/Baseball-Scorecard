// Run: node --test .agents/workflows/tests/
// Runs the wave script with stubbed globals for a card in Review and checks the
// truth-check agents the wave starts and what the Reviewer is told (#48).
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const read = (rel) => readFileSync(fileURLToPath(new URL(rel, import.meta.url)), 'utf8')
const src = read('../super-board-wave.js').replace('export const meta', 'const meta')
const run = new Function('args', 'agent', 'pipeline', 'log', `return (async () => { ${src} })()`)

// The fixed brief, straight from super-review → "Adversarial mode".
const skill = read('../../skills/super-review/SKILL.md')
const BRIEF = skill.split('**Use this brief verbatim.**')[1].split('```')[1].trim()
const squash = (s) => s.replace(/\s+/g, ' ').trim()
const escape = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
// Every fixed part of the brief, in order; each <slot> may hold anything.
const briefPattern = new RegExp(squash(BRIEF).split(/<[^>]*>/).map((p) => escape(p)).join('[\\s\\S]*?'))

const CARD = { number: 40, status: 'Review', title: 't', lane: 'review', labels: ['bug'] }
const FIRES = {
  status: 'fires', detail: '42 changed lines', pr: 41, threshold: 70,
  acs: '- [ ] AC one', diff: 'git diff origin/staging...origin/issue-40-x', claims: 'builder says it works',
}

async function wave({ gate = FIRES, grounder = { status: 'ok', confidence: 90, findings: [] }, historian = { status: 'ok', confidence: 60, findings: [] } } = {}) {
  const calls = []
  const agent = async (prompt, opts) => {
    calls.push({ label: opts.label, prompt })
    if (opts.label.startsWith('truth-gate:')) return gate
    if (opts.label.startsWith('truth:code-grounder:')) return grounder
    if (opts.label.startsWith('truth:historian:')) return historian
    return { status: 'advanced', column: 'Done', detail: 'merged' }
  }
  const pipeline = async (items, ...stages) => Promise.all(items.map(async (item) => {
    let prev
    for (const stage of stages) prev = await stage(prev === undefined ? item : prev, item)
    return prev
  }))
  const out = await run({ configPath: 'c.json', cards: [CARD] }, agent, pipeline, () => {})
  const review = calls.find((c) => c.label === 'review:#40')
  return { calls, out, review: review && review.prompt, labels: calls.map((c) => c.label) }
}

test('gate fires: Code-grounder and Historian run as two agents with the verbatim brief', async () => {
  const { calls, labels } = await wave()
  assert.ok(labels.includes('truth:code-grounder:#40'))
  assert.ok(labels.includes('truth:historian:#40'))
  const grounder = calls.find((c) => c.label === 'truth:code-grounder:#40').prompt
  const historian = calls.find((c) => c.label === 'truth:historian:#40').prompt
  for (const p of [grounder, historian]) {
    assert.match(squash(p), briefPattern)
    assert.ok(p.includes('PR #41'))
    assert.ok(p.includes('- [ ] AC one'))
    assert.ok(p.includes('git diff origin/staging...origin/issue-40-x'))
    assert.ok(p.includes('builder says it works'))
  }
  assert.ok(grounder.startsWith('You are the Code-grounder in an adversarial truth-check of PR #41.'))
  assert.ok(squash(grounder).includes('Job: verify every file:line the PR cites still exists and does what the PR claims.'))
  assert.ok(historian.startsWith('You are the Historian in an adversarial truth-check of PR #41.'))
  assert.ok(squash(historian).includes('Job: git blame the changed lines; look for ADRs, prior incidents, reverted attempts.'))
})

test('the Reviewer gets the minimum score, the threshold and the agents that gave it', async () => {
  const { review } = await wave()
  assert.ok(review, 'review lane ran')
  assert.ok(review.includes('truth:code-grounder:#40 90/100'))
  assert.ok(review.includes('truth:historian:#40 60/100'))
  assert.ok(review.includes('combined (minimum) 60/100 vs truth_threshold 70: below'))
  assert.ok(review.includes('Reviewer report comment'))
  assert.ok(!review.includes('NO independent truth score'))
})

test('a truth agent that returns nothing leaves no independent score: no merge', async () => {
  const { review } = await wave({ historian: null })
  assert.ok(review.includes('NO independent truth score'))
  assert.ok(review.includes('Do NOT run super-board-merge-gate.sh'))
  assert.ok(!review.includes('combined (minimum)'))
})

test('insufficient data counts as no independent score', async () => {
  const { review } = await wave({ grounder: { status: 'ok', insufficientData: true, findings: [] } })
  assert.ok(review.includes('NO independent truth score'))
})

test('a gate agent that returns nothing fails closed: no truth agents, no merge', async () => {
  const { labels, review } = await wave({ gate: null })
  assert.ok(!labels.some((l) => l.startsWith('truth:')))
  assert.ok(review.includes('NO independent truth score'))
})

test('gate skips (trivial diff or truth_gate off): no truth agents start', async () => {
  const { labels, review } = await wave({ gate: { status: 'skip', detail: 'truth_gate off' } })
  assert.ok(labels.includes('truth-gate:#40'))
  assert.ok(!labels.some((l) => l.startsWith('truth:')))
  assert.ok(review.includes('Truth gate skipped by this wave (truth_gate off)'))
  assert.ok(!review.includes('NO independent truth score'))
})

test('a halted truth agent halts the run before Review', async () => {
  const { review, out } = await wave({ grounder: { status: 'halted', detail: 'exit 79' } })
  assert.equal(review, undefined)
  assert.equal(out.halted, true)
})

// QA gap tests (#48 test-gap check): threshold boundary, gate edge cases.
const ok = (confidence) => ({ status: 'ok', confidence, findings: [] })

test('a minimum equal to the threshold is at or above it; one below is below', async () => {
  const at = (await wave({ grounder: ok(70), historian: ok(95) })).review
  assert.ok(at.includes('combined (minimum) 70/100 vs truth_threshold 70: at or above'))
  const below = (await wave({ grounder: ok(69), historian: ok(95) })).review
  assert.ok(below.includes('combined (minimum) 69/100 vs truth_threshold 70: below'))
})

test('the threshold comes from the gate (config), 70 only when it gave none', async () => {
  const strict = (await wave({ gate: { ...FIRES, threshold: 80 }, grounder: ok(75), historian: ok(90) })).review
  assert.ok(strict.includes('combined (minimum) 75/100 vs truth_threshold 80: below'))
  const { threshold, ...noThreshold } = FIRES
  const dflt = (await wave({ gate: noThreshold, grounder: ok(75), historian: ok(90) })).review
  assert.ok(dflt.includes('combined (minimum) 75/100 vs truth_threshold 70: at or above'))
})

test('a gate that fires without a PR number starts no truth agents and gives no score', async () => {
  const { pr, ...noPr } = FIRES
  const { labels, review } = await wave({ gate: noPr })
  assert.ok(!labels.some((l) => l.startsWith('truth:')))
  assert.ok(review.includes('NO independent truth score'))
  assert.ok(review.includes('truth-gate agent gave no PR'))
})

test('a non-integer confidence counts as no independent score', async () => {
  const { review } = await wave({ grounder: { status: 'ok', confidence: '90', findings: [] } })
  assert.ok(review.includes('NO independent truth score'))
  assert.ok(review.includes('truth:code-grounder:#40 gave no score'))
  assert.ok(!review.includes('combined (minimum)'))
})

test('a halted gate halts the run before any truth agent or Review', async () => {
  const { labels, review, out } = await wave({ gate: { status: 'halted', detail: 'exit 79' } })
  assert.ok(!labels.some((l) => l.startsWith('truth:')))
  assert.equal(review, undefined)
  assert.equal(out.halted, true)
})

test('the gate agent gets the truth_gate rule and writes nothing', async () => {
  const { calls } = await wave({ gate: { status: 'skip', detail: 'trivial' } })
  const gate = calls.find((c) => c.label === 'truth-gate:#40').prompt
  assert.ok(gate.includes('off → skip · always → fires · non-trivial (default) → fires when those non-test changed lines ≥ truth_min_lines or a label is security, migration, payments or auth; otherwise skip.'))
  assert.ok(gate.includes('truth_min_lines (default 10)'))
  assert.ok(gate.includes('leaving out test files'))
  assert.ok(gate.includes('You write nothing'))
  assert.ok(gate.includes('Do not build or run tests'))
})

// A Ready card runs Pre-flight, Classify, Build, QA, then Review: Review takes
// the same ladder rung as Build/QA, not the session model.
async function readyWave(complexity, tier) {
  const calls = []
  const agent = async (prompt, opts) => {
    calls.push({ label: opts.label, model: opts.model })
    if (opts.label.startsWith('preflight:')) return { verdicts: [{ number: 40, verdict: 'proceed', column: 'Ready', detail: 'ok' }] }
    if (opts.label.startsWith('classify:')) return { kind: 'feature', complexity }
    if (opts.label.startsWith('truth-gate:')) return { status: 'skip', detail: 'small' }
    return { status: 'advanced', column: 'Done', detail: 'ok' }
  }
  const pipeline = async (items, ...stages) => Promise.all(items.map(async (item) => {
    let prev
    for (const stage of stages) prev = await stage(prev === undefined ? item : prev, item)
    return prev
  }))
  const card = { number: 40, status: 'Ready', title: 't', lane: 'build', labels: ['feature'] }
  await run({ configPath: 'c.json', cards: [card], ...(tier ? { tier } : {}) }, agent, pipeline, () => {})
  return (label) => calls.find((c) => c.label === label)
}

test('Review uses the card\'s ladder model, like Build and QA', async () => {
  const low = await readyWave('low')
  assert.equal(low('build:#40').model, 'sonnet')
  assert.equal(low('review:#40').model, 'sonnet')
  const medium = await readyWave('medium')
  assert.equal(medium('review:#40').model, 'opus')
  const high = await readyWave('high')
  assert.equal(high('review:#40').model, undefined)
})

test('a card that enters at Review (never classified) keeps the session model', async () => {
  const calls = []
  const agent = async (prompt, opts) => {
    calls.push({ label: opts.label, model: opts.model })
    if (opts.label.startsWith('truth-gate:')) return { status: 'skip', detail: 'small' }
    return { status: 'advanced', column: 'Done', detail: 'ok' }
  }
  const pipeline = async (items, ...stages) => Promise.all(items.map(async (item) => {
    let prev
    for (const stage of stages) prev = await stage(prev === undefined ? item : prev, item)
    return prev
  }))
  await run({ configPath: 'c.json', cards: [CARD] }, agent, pipeline, () => {})
  assert.equal(calls.find((c) => c.label === 'review:#40').model, undefined)
})

test('the Reviewer leaves the suite rerun to the merge gate, except where the gate does not verify', async () => {
  const { review } = await wave()
  assert.ok(review.includes('Do not rerun the full suite before the merge gate'))
  assert.ok(review.includes('the gate exits 7 or 8 (it stops before verifying)'))
})
