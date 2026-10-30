import React, {useEffect, useState} from 'react';
import {createRoot} from 'react-dom/client';
import {MainContainer, ChatContainer, MessageList, Message, TypingIndicator} from '@chatscope/chat-ui-kit-react';
import '@chatscope/chat-ui-kit-styles/dist/default/styles.min.css';
import './chat.css';

function Chat(){
  const [messages,setMessages]=useState([]),[busy,setBusy]=useState(false),[error,setError]=useState('');
  useEffect(()=>{const bridge=window.chrome?.webview;if(!bridge)return;const receive=e=>{const m=e.data;if(m?.type==='user'){setMessages(old=>[...old,{role:'user',text:m.text}]);setBusy(true);setError('');}else if(m?.type==='answer'){setMessages(old=>[...old,{role:'assistant',text:m.text}]);setBusy(false);setError('');}else if(m?.type==='error'){setBusy(false);setError(m.text);}else if(m?.type==='clear'){setMessages([]);setBusy(false);setError('');}};bridge.addEventListener('message',receive);bridge.postMessage({action:'ready'});return()=>bridge.removeEventListener('message',receive)},[]);
  return <div className="page"><header><div className="mark">✦</div><div><h1>Conversation locale</h1><p>Posez vos questions sur votre poste, en toute simplicité.</p></div><span className="local">● Local</span></header><main><MainContainer><ChatContainer><MessageList typingIndicator={busy?<TypingIndicator content="Génération en cours…"/>:undefined}>{messages.length===0&&<div className="empty"><div className="spark">✦</div><h2>Comment puis-je vous aider ?</h2><p>Explorez vos idées ou préparez des questions pour vos agents.</p></div>}{messages.map((m,i)=><Message key={i} model={{direction:m.role==='user'?'outgoing':'incoming',position:'single',type:'custom',sender:m.role==='user'?'Vous':'Assistant'}}><Message.CustomContent><span className="plain">{m.text}</span></Message.CustomContent></Message>)}</MessageList>{error&&<div className="error">{error}</div>}</ChatContainer></MainContainer></main></div>;
}
createRoot(document.getElementById('root')).render(<Chat/>);
