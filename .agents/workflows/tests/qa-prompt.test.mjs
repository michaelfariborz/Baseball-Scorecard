// Run: node --test .agents/workflows/tests/
// Runs the wave script with stubbed globals and captures the QA lane prompt.
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const src = readFileSync(fileURLToPath(new URL('../super-board-wave.js', import.meta.url)), 'utf8')
  .replace('export const meta', 'const meta')
const run = new Function('args', 'agent', 'pipeline', 'log', `return (async () => { ${src} })()`)

const LABELLED_QA = 'This card is labelled qa'

async function qaPrompt(card) {
  const prompts = []
  const agent = async (prompt, opts) => {
    prompts.push({ label: opts.label, prompt })
    if (opts.label.startsWith('preflight')) return { verdicts: [{ number: card.number, verdict: 'proceed', detail: 'ok' }] }
    if (opts.label.startsWith('classify')) return { kind: 'feature', complexity: 'low' }
    if (opts.label.startsWith('build')) return { status: 'advanced', column: 'QA', detail: 'built' }
    return { status: 'bounced', column: 'Ready', detail: 'stop' }
  }
  const pipeline = async (items, ...stages) => Promise.all(items.map(async (item) => {
    let prev
    for (const stage of stages) prev = await stage(prev === undefined ? item : prev, item)
    return prev
  }))
  await run({ configPath: 'c.json', cards: [card] }, agent, pipeline, () => {})
  return prompts.find((p) => p.label.startsWith('qa:'))?.prompt
}

test('a built feature card that reads Ready does not get the labelled-qa line', async () => {
  const prompt = await qaPrompt({ number: 30, status: 'Ready', title: 't', lane: 'build', labels: ['feature'] })
  assert.ok(prompt, 'QA lane ran')
  assert.ok(!prompt.includes(LABELLED_QA))
})

test('a card labelled qa at Ready gets the labelled-qa line', async () => {
  const prompt = await qaPrompt({ number: 31, status: 'Ready', title: 't', lane: 'qa', labels: ['qa'] })
  assert.ok(prompt.includes(LABELLED_QA))
})
