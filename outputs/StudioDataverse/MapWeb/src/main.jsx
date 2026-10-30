import React, { useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { ReactFlow, ReactFlowProvider, Background, Controls, MiniMap, Panel, useNodesState, useReactFlow, useNodesInitialized } from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import './style.css';
import { createNodes, filterAgents } from './layout.mjs';

const send = (action, id) => window.chrome?.webview?.postMessage({ action, id });
function AgentNode({ data }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [data.icon]);
  return <div className="agent-node" title={`${data.name}\n${data.status} · ${data.language}`}>
    <div className="avatar">{data.icon && !failed ? <img alt="" src={`data:image/png;base64,${data.icon}`} onError={() => setFailed(true)} /> : <svg viewBox="0 0 32 32" aria-hidden="true"><path d="m13 5-7 11 7 11h7l7-11L20 5h-7Zm0 0 7 22M20 5l-7 22" fill="none" stroke="#008be5" strokeWidth="2" strokeLinejoin="round" /></svg>}</div>
    <span>{data.name}</span>
  </div>;
}
function ClusterNode({ data }) { return <div className="cluster"><div className="cluster-label"><strong>{data.name}</strong><b>{data.total}</b><span>(Affichage de {data.displayed}) <small>ⓘ</small></span></div></div>; }
const nodeTypes = { agent: AgentNode, cluster: ClusterNode };
function Map() {
  const [payload, setPayload] = useState({ agents: [], total: null, connected: false });
  const [filters, setFilters] = useState({ search: '', state: '', publication: '', managed: '' });
  const [groupBy, setGroupBy] = useState('platform');
  const [sidebar, setSidebar] = useState(true);
  const [miniMap, setMiniMap] = useState(false);
  const [context, setContext] = useState(null);
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const { fitView } = useReactFlow();
  const nodesInitialized = useNodesInitialized();
  useEffect(() => {
    const receive = event => { if (event.data?.type === 'agents' && Array.isArray(event.data.agents)) { setPayload(event.data); setContext(null); } };
    window.chrome?.webview?.addEventListener('message', receive);
    send('ready');
    return () => window.chrome?.webview?.removeEventListener('message', receive);
  }, []);
  const agents = useMemo(() => filterAgents(payload.agents, filters), [payload.agents, filters]);
  useEffect(() => setNodes(createNodes(agents, groupBy)), [agents, groupBy, setNodes]);
  useEffect(() => { if (!nodesInitialized) return; const frame = requestAnimationFrame(() => fitView({ padding: .14, duration: 250 })); return () => cancelAnimationFrame(frame); }, [nodesInitialized, agents, groupBy, fitView]);
  const filter = (name, value) => setFilters(f => ({ ...f, [name]: value }));
  const selectFilter = (title, name, options) => <details><summary>{title}<span>⌄</span></summary><select aria-label={title} value={filters[name]} onChange={e => filter(name, e.target.value)}>{options.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></details>;
  const runContext = action => { send(action, context.id); setContext(null); };
  return <div className="map-shell" onContextMenu={e => e.preventDefault()}>
    <div className="map-tab"><span>◈</span> Tous les assistants</div>
    <div className="map-body">
      <ReactFlow nodes={nodes} edges={[]} nodeTypes={nodeTypes} onNodesChange={onNodesChange} nodesConnectable={false} deleteKeyCode={null} minZoom={.2} maxZoom={2.5} fitView fitViewOptions={{ padding: .14 }} onNodeClick={(_, node) => node.type === 'agent' && send('open', node.id)} onNodeContextMenu={(event, node) => { if (node.type !== 'agent') return; event.preventDefault(); const rect = event.currentTarget.closest('.map-body').getBoundingClientRect(); setContext({ id: node.id, x: Math.max(4, Math.min(event.clientX - rect.left, rect.width - 258)), y: Math.max(4, Math.min(event.clientY - rect.top, rect.height - 295)) }); }} onPaneClick={() => setContext(null)}>
        <Background color="#dce2eb" gap={20} size={.7} />
        <Controls position="top-right" showInteractive={false} />
        <Panel position="top-right" className="map-tools"><button onClick={() => setSidebar(s => !s)} title="Afficher ou masquer les filtres">☷</button><button onClick={() => fitView({ padding: .14, duration: 250 })} title="Centrer la carte">⊙</button><button onClick={() => setMiniMap(s => !s)} title="Mini-carte">▧</button></Panel>
        {miniMap && <MiniMap pannable zoomable nodeColor={n => n.type === 'cluster' ? '#f8e7d7' : '#0078d4'} maskColor="#edf1f5aa" />}
      </ReactFlow>
      {sidebar && <aside className="filters"><header><strong>Filtres</strong><button title="Masquer les filtres" onClick={() => setSidebar(false)}>⇤</button></header><label className="search"><span>⌕</span><input placeholder="Recherchez des assistants…" value={filters.search} onChange={e => filter('search', e.target.value)} /></label>
        <div className="stats"><div className="stat selected"><span>Nombre total<br/>d’agents ⓘ</span><b>{payload.total ?? payload.agents.length}</b></div><div className="stat" title="La source Dataverse ne fournit pas cet indicateur Agent 365"><span>Assistants à risque ⓘ</span><b>—</b></div><div className="stat" title="Aucune évaluation des propriétaires absents n’est disponible"><span>Agents sans<br/>propriétaire ⓘ</span><b>—</b></div><div className="stat" title="Champ ismanaged=false ; différent du statut Agent 365 non géré"><span>Solutions<br/>non managées</span><b>{payload.agents.filter(a => a.managed === false).length}</b></div></div>
        <p className="caption">Cluster par</p><div className="segments">{[['platform', 'Plateforme'], ['state', 'État'], ['language', 'Langue']].map(([value, label]) => <button key={value} className={groupBy === value ? 'active' : ''} onClick={() => setGroupBy(value)}>{label}</button>)}</div>
        <p className="caption">Filtrer par</p>{selectFilter('Statut', 'state', [['', 'Tous les états'], ['0', 'Actif'], ['1', 'Inactif']])}{selectFilter('Type de solution', 'managed', [['', 'Toutes'], ['no', 'Non managée'], ['yes', 'Managée']])}<details><summary>Plateforme<span>⌄</span></summary><p>Copilot Studio · environnement connecté</p></details>{selectFilter('Publication', 'publication', [['', 'Toutes'], ['yes', 'Déjà publié'], ['no', 'Jamais publié']])}
        <button className="reset" onClick={() => { setFilters({ search: '', state: '', publication: '', managed: '' }); setGroupBy('platform'); }}>Réinitialiser</button><p className="source">{agents.length} résultat(s) · {payload.agents.length} agents chargés.<br/>Les tirets signalent une donnée indisponible.</p>
      </aside>}
      {agents.length === 0 && <div className="empty">{payload.connected ? 'Aucun agent correspondant aux filtres.' : 'Connectez-vous à Microsoft pour charger les agents.'}</div>}
      {context && <div className="context-menu" style={{ left: context.x, top: context.y }}>{[['transcripts', 'Voir les transcriptions'], ['components', 'Voir les composants'], ['open', 'Afficher les détails'], ['favorite', 'Ajouter / retirer des favoris'], ['copy', 'Copier l’identifiant'], ['reference', 'Référence de comparaison']].map(([action, label]) => <button key={action} onClick={() => runContext(action)}>{label}</button>)}</div>}
      <footer className="map-source">React Flow · données Dataverse · groupes visuels, sans connexions techniques supposées</footer>
    </div>
  </div>;
}
createRoot(document.getElementById('root')).render(<ReactFlowProvider><Map /></ReactFlowProvider>);
