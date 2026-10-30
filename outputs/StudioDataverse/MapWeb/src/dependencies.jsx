import React, {useEffect, useMemo, useState} from 'react';
import {createRoot} from 'react-dom/client';
import {ReactFlow, ReactFlowProvider, Background, Controls, MiniMap, Handle, Position, useReactFlow} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import './dependencies.css';
import {graphLayout, upstreamAgents, visibleGraph} from './dependencies.mjs';
const send=(action,id)=>window.chrome?.webview?.postMessage({action,id});
function ObjectNode({data}) {
  return <div className={'object '+data.kindClass} title={data.label+' · '+data.state}>
    <Handle type="target" position={Position.Left}/><small>{data.kind}</small><strong>{data.label}</strong><span>{data.state}</span><Handle type="source" position={Position.Right}/>
  </div>;
}
const types={object:ObjectNode};
function Graph() {
  const [payload,setPayload]=useState({nodes:[],edges:[]}),[search,setSearch]=useState(''),[kind,setKind]=useState(''),[selected,setSelected]=useState('');
  const {fitView}=useReactFlow();
  useEffect(()=>{const listener=event=>{if(event.data?.type==='dependencies'&&Array.isArray(event.data.nodes)&&Array.isArray(event.data.edges)){setPayload(event.data);setSelected('');}};
    window.chrome?.webview?.addEventListener('message',listener);send('ready');return()=>window.chrome?.webview?.removeEventListener('message',listener);},[]);
  const view=useMemo(()=>visibleGraph(payload,search,kind),[payload,search,kind]);
  const graph=useMemo(()=>graphLayout(view),[view]);
  const impacted=useMemo(()=>upstreamAgents(payload,selected),[payload,selected]);
  useEffect(()=>{const timer=setTimeout(()=>fitView({padding:.2,duration:180}),100);return()=>clearTimeout(timer);},[graph,fitView]);
  return <div className="dependency-shell"><header><div><b>Dépendances observées</b><small>{view.nodes.length} objets · {view.edges.length} liens</small></div>
    <input aria-label="Rechercher un objet" placeholder="Agent, flux, connexion, source…" value={search} onChange={e=>setSearch(e.target.value)}/>
    <select aria-label="Type d’objet" value={kind} onChange={e=>setKind(e.target.value)}><option value="">Tous les types</option>{[...new Set(payload.nodes.map(n=>n.kind))].sort().map(k=><option key={k}>{k}</option>)}</select></header>
    <ReactFlow nodes={graph.nodes.map(n=>({...n,style:{opacity:selected&&!impacted.has(n.id)&&selected!==n.id?.toString()? .65:1},selected:n.id===selected}))} edges={graph.edges} nodeTypes={types} nodesConnectable={false} deleteKeyCode={null} minZoom={.12} maxZoom={2.5} fitView onNodeClick={(_,node)=>{setSelected(node.id);send('select',node.id);}} onPaneClick={()=>setSelected('')}>
      <Background gap={20} color="#dde2ed"/><Controls showInteractive={false}/><MiniMap pannable zoomable nodeColor={n=>n.data.kind==='agent'?'#6357d5':n.data.kind==='flux'?'#0078d4':'#00a393'}/>
    </ReactFlow><footer>{selected?impacted.size+' agent(s) potentiellement impacté(s) par cet objet.':'Sélectionnez un objet pour explorer son impact. Les liens sont déclarés, pas des appels observés en exécution.'}</footer></div>;
}
createRoot(document.getElementById('root')).render(<ReactFlowProvider><Graph/></ReactFlowProvider>);
