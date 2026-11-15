// Run: node --test .agents/bin/tests/*.test.mjs
// Offline: saved --from (deps) and --items (board) payloads, no GitHub calls.
// Covers issue #84 — a human dragging a `blocked-by: -` card to Ready releases it,
// while the Blocked → Ready sweep still never frees such a card.
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const BIN = fileURLToPath(new URL('..', import.meta.url))
const dir = mkdtempSync(join(tmpdir(), 'sb-wave-plan-'))

const block = (line) => ({ body: `[review] [blocker] 🛑 blocked\nReason tag: ❓\n${line}` })
const issue = (number, status, comments, body = '## Blocked by\n- None.\n') =>
  // GraphQL connection shape, as the live deps query returns it.
  ({ number, status, title: `card ${number}`, body, state: 'OPEN',
     labels: { nodes: [] }, comments: { nodes: comments } })

const ISSUES = [
  issue(12, 'Backlog', []),
  issue(80, 'Ready', [block('blocked-by: -')]),     // dragged back to Ready by a human
  issue(81, 'Blocked', [block('blocked-by: -')]),   // still waiting on a person
  issue(82, 'Ready', [block('blocked-by: 12')]),    // waiting on open #12
]

function write(name, value) {
  const path = join(dir, name)
  writeFileSync(path, JSON.stringify(value))
  return path
}

const from = write('issues.json', ISSUES.map(({ status, ...i }) => i))
const items = write('items.json', {
  items: ISSUES.map(({ number, title, status }) => ({
    status, labels: ['bug'], content: { type: 'Issue', number, title, assignees: [] },
  })),
})
const config = write('config.json', {
  project: { owner: 'o', number: 1 }, repo: { path: dir }, max_workers: 0,
  verify_commands: ['true'],
})

const deps = JSON.parse(execFileSync('bash', [join(BIN, 'super-board-deps.sh'), '--from', from], { encoding: 'utf8' }))
const depsFile = write('deps.json', deps)
const plan = JSON.parse(execFileSync('bash', [
  join(BIN, 'super-board-wave-plan.sh'), '--config', config, '--items', items, '--deps', depsFile,
], { encoding: 'utf8', env: { ...process.env, SB_REPO_PATH: dir } }))
const nums = (list) => list.map((c) => c.number)

test('deps still reports blocked-by: - as human-gated, column-blind', () => {
  assert.equal(deps['80'].humanGated, true)
  assert.equal(deps['80'].runnable, false)
  assert.deepEqual(deps['80'].openBlockers, [])
})

test('Ready card whose newest comment is blocked-by: - is planned with its lane', () => {
  const card = plan.cards.find((c) => c.number === 80)
  assert.ok(card, `#80 missing from cards: ${JSON.stringify(plan.cards)}`)
  assert.equal(card.lane, 'build')
})

test('Blocked card with blocked-by: - stays out of sweep and cards', () => {
  assert.ok(!nums(plan.sweep).includes(81))
  assert.ok(!nums(plan.cards).includes(81))
})

test('Ready card naming an open blocker stays out of cards', () => {
  assert.ok(!nums(plan.cards).includes(82))
})

test('a gated Ready card is not flagged as unreadable', () => {
  assert.ok(!nums(plan.flag).includes(80))
})
