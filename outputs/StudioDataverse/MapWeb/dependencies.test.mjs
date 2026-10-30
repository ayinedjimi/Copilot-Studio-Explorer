import test from 'node:test';
import assert from 'node:assert/strict';
import {upstreamAgents,visibleGraph,graphLayout} from './src/dependencies.mjs';
const graph={nodes:[{id:'agent:1',kind:'agent',label:'Agent A',state:'Accessible'},{id:'component:1',kind:'outil/rubrique',label:'Outil',state:'Déclaré'},{id:'flow:1',kind:'flux',label:'Créer ticket',state:'Accessible'},{id:'connection:mail',kind:'connexion',label:'Mail',state:'Accessible'},{id:'agent:2',kind:'agent',label:'Agent isolé',state:'Accessible'}],edges:[{id:'a',source:'agent:1',target:'component:1'},{id:'b',source:'component:1',target:'flow:1'},{id:'c',source:'flow:1',target:'connection:mail'}]};
test('impact traces only actual upstream agents',()=>assert.deepEqual([...upstreamAgents(graph,'connection:mail')],['agent:1']));
test('cycles terminate without fabricated impact',()=>assert.deepEqual([...upstreamAgents({...graph,edges:[...graph.edges,{id:'cycle',source:'connection:mail',target:'flow:1'}]},'flow:1')],['agent:1']));
test('search retains matching objects and their ancestors',()=>assert.deepEqual(visibleGraph(graph,'mail').nodes.map(n=>n.id),['agent:1','component:1','flow:1','connection:mail']));
test('kind filter retains explanatory incoming path',()=>assert.equal(visibleGraph(graph,'','flux').nodes.length,3));
test('layout preserves observed edge count and rejects dangling edges',()=>{const result=graphLayout({...graph,edges:[...graph.edges,{id:'fake',source:'missing',target:'agent:1'}]});assert.equal(result.edges.length,3);assert.equal(result.nodes.length,5);});
