import { execFileSync } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = fileURLToPath(new URL('../', import.meta.url));
const outputDirectory = resolve(process.argv[2] ?? `${repositoryRoot}/dist/erd`);
const git = (...args) => execFileSync('git', args, {
  cwd: repositoryRoot,
  encoding: 'utf8',
}).trim();

const repositoryUrl = git('remote', 'get-url', 'origin')
  .replace(/^git@github.com:/, 'https://github.com/')
  .replace(/\.git$/, '');
if (!repositoryUrl.startsWith('https://github.com/')) {
  throw new Error('Remote origin phải là repository GitHub để tạo liên kết source.');
}

const revision = git('rev-parse', 'HEAD');
const html = await readFile(`${repositoryRoot}/docs/database-erd.html`, 'utf8');
const site = html.replaceAll('../backend/Fookbase.Src/',
  `${repositoryUrl}/blob/${revision}/backend/Fookbase.Src/`);

await mkdir(outputDirectory, { recursive: true });
await writeFile(`${outputDirectory}/index.html`, site, 'utf8');
console.log(`Đã tạo ${outputDirectory}/index.html · source ${revision}`);
