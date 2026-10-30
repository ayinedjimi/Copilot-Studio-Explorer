import { build } from 'esbuild';
import { copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
await mkdir('../Assets/AgentMap', { recursive: true });
await build({ absWorkingDir: fileURLToPath(new URL('.', import.meta.url)), entryPoints: [fileURLToPath(new URL('src/main.jsx', import.meta.url))], tsconfigRaw: {}, bundle: true, minify: true, format: 'esm', target: 'chrome110', outfile: '../Assets/AgentMap/app.js', loader: { '.css': 'css' }, legalComments: 'linked' });
await copyFile('index.html', '../Assets/AgentMap/index.html');
const lock = JSON.parse(await readFile('package-lock.json', 'utf8'));
const notices = [];
for (const [path, info] of Object.entries(lock.packages)) {
  if (!path || info.dev) continue;
  for (const name of ['LICENSE', 'LICENSE.md', 'LICENSE.txt']) {
    try { notices.push(`${path} ${info.version}\n${await readFile(`${path}/${name}`, 'utf8')}`); break; } catch {}
  }
}
await writeFile('../Assets/AgentMap/THIRD-PARTY-NOTICES.txt', notices.join('\n\n--------------------\n\n'));

await build({ absWorkingDir: fileURLToPath(new URL('.', import.meta.url)), entryPoints: [fileURLToPath(new URL('src/dependencies.jsx', import.meta.url))], tsconfigRaw: {}, bundle: true, minify: true, format: 'esm', target: 'chrome110', outfile: '../Assets/AgentMap/dependency.js', loader: { '.css': 'css' }, legalComments: 'linked' });
await copyFile('dependencies.html', '../Assets/AgentMap/dependencies.html');
await build({ absWorkingDir: fileURLToPath(new URL('.', import.meta.url)), entryPoints: [fileURLToPath(new URL('src/chat.jsx', import.meta.url))], tsconfigRaw: {}, bundle: true, minify: true, format: 'esm', target: 'chrome110', outfile: '../Assets/AgentMap/chat.js', loader: { '.css': 'css' }, legalComments: 'linked' });
await copyFile('chat.html', '../Assets/AgentMap/chat.html');
