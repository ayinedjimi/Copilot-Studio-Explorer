export function filterAgents(agents, { search = '', state = '', publication = '', managed = '' } = {}) {
  const terms = search.trim().toLocaleLowerCase('fr').split(/\s+/).filter(Boolean);
  return agents.filter(a => terms.every(term => `${a.name} ${a.schema} ${a.id}`.toLocaleLowerCase('fr').includes(term)) &&
    (state === '' || a.state === Number(state)) && (publication === '' || a.published === (publication === 'yes')) &&
    (managed === '' || a.managed === (managed === 'yes')));
}
export function createNodes(agents, groupBy = 'platform') {
  const groups = new Map();
  for (const a of agents) {
    const key = groupBy === 'state' ? a.status : groupBy === 'language' ? a.language : groupBy === 'managed' ? a.managed === true ? 'Solution managée' : a.managed === false ? 'Solution non managée' : 'Solution inconnue' : 'Copilot Studio';
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(a);
  }
  const nodes = [];
  [...groups].sort(([a], [b]) => a.localeCompare(b, 'fr')).forEach(([name, items], g) => {
    const id = `group-${g}`;
    const visible = items.slice(0, 100);
    nodes.push({ id, type: 'cluster', position: { x: (g % 2) * 1140 + 200, y: Math.floor(g / 2) * 1140 }, data: { name, total: items.length, displayed: visible.length }, style: { width: 1050, height: 1050 }, draggable: false, selectable: false, zIndex: 0 });
    visible.forEach((a, i) => {
      const angle = i * 2.3999632297, radius = 240 + 200 * Math.sqrt((i + .5) / visible.length);
      nodes.push({ id: a.id, type: 'agent', parentId: id, extent: 'parent', position: { x: 525 + Math.cos(angle) * radius - 58, y: 525 + Math.sin(angle) * radius - 35 }, data: a, zIndex: 1 });
    });
  });
  return nodes;
}
