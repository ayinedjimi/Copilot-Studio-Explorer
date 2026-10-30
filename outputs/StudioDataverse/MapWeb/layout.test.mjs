import { test } from 'node:test';
import assert from 'node:assert/strict';
import { filterAgents, createNodes } from './src/layout.mjs';
const agents = Array.from({ length: 130 }, (_, i) => ({ id: `id-${i}`, name: `Agent ${i}`, schema: `demo_${i}`, state: i % 2, status: i % 2 ? 'Inactif' : 'Actif', language: i % 2 ? 'Français' : 'English', published: i % 3 === 0, managed: i % 2 === 0 }));
test('combined filters and multiword search', () => assert.deepEqual(filterAgents(agents, { search: 'agent demo_12', state: '0', managed: 'yes' }).map(a => a.id), ['id-12', 'id-120', 'id-122', 'id-124', 'id-126', 'id-128']));
test('100 visible nodes per group with actual counts', () => { const nodes = createNodes(agents); assert.equal(nodes.length, 101); assert.equal(nodes[0].data.total, 130); assert.equal(nodes[0].data.displayed, 100); });
test('parent nodes precede children and positions stay in groups', () => { const nodes = createNodes(agents, 'state'); for (const n of nodes.filter(n => n.type === 'agent')) { assert(nodes.findIndex(p => p.id === n.parentId) < nodes.indexOf(n)); assert(n.position.x >= 0 && n.position.x <= 934 && n.position.y >= 0 && n.position.y <= 980); } });
test('no fabricated edges or external platforms', () => { const nodes = createNodes(agents); assert.equal(nodes[0].data.name, 'Copilot Studio'); assert(!nodes.some(n => 'source' in n || 'target' in n)); });
test('publication filter distinguishes false and missing values', () => assert.equal(filterAgents([{ ...agents[0], published: false }, { ...agents[1], published: null }], { publication: 'no' }).length, 1));
