export function upstreamAgents(graph,target){
 const visited=new Set(target?[target]:[]),queue=target?[target]:[];
 for(let i=0;i<queue.length;i++)for(const e of graph.edges)if(e.target===queue[i]&&!visited.has(e.source)){visited.add(e.source);queue.push(e.source);}
 return new Set(graph.nodes.filter(n=>n.kind==='agent'&&visited.has(n.id)).map(n=>n.id));
}
export function visibleGraph(graph,search='',kind=''){
 if(!search.trim()&&!kind)return graph;
 const q=search.trim().toLocaleLowerCase('fr');
 const included=new Set(graph.nodes.filter(n=>(!kind||n.kind===kind)&&((n.label+' '+n.state).toLocaleLowerCase('fr').includes(q))).map(n=>n.id));
 const queue=[...included];
 for(let i=0;i<queue.length;i++)for(const e of graph.edges)if(e.target===queue[i]&&!included.has(e.source)){included.add(e.source);queue.push(e.source);}
 return {nodes:graph.nodes.filter(n=>included.has(n.id)),edges:graph.edges.filter(e=>included.has(e.source)&&included.has(e.target))};
}
export function graphLayout(graph){
 const rank=n=>n.kind==='agent'?0:n.id.startsWith('component:')?1:n.kind==='flux'?2:3;
 const counts=[0,0,0,0],ids=new Set(graph.nodes.map(n=>n.id));
 return {nodes:[...graph.nodes].sort((a,b)=>rank(a)-rank(b)||a.label.localeCompare(b.label,'fr')).map(n=>{
   const column=rank(n),row=counts[column]++;
   return {id:n.id,type:'object',position:{x:column*310,y:row*112},data:{...n,kindClass:n.kind==='agent'?'agent':n.kind==='flux'?'flow':n.kind==='connexion'?'connection':'other'}};
 }),edges:graph.edges.filter(e=>ids.has(e.source)&&ids.has(e.target)).map(e=>({...e,type:'smoothstep',animated:false,style:{stroke:'#8680b7'},labelStyle:{fontSize:10},markerEnd:{type:'arrowclosed',color:'#8680b7'}}))};
}
