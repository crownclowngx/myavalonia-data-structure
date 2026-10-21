import { createHash } from 'node:crypto';
import { readFile, writeFile, mkdir, utimes } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
import { execSync } from 'node:child_process';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../..');
const outputOption = process.argv.indexOf('--output');
const output = outputOption < 0 ? path.join(root, 'src/DataStructurePlugin.Plugin/obj/ac-tutorial-assets') : path.resolve(process.argv[outputOption + 1]);
const markdown = (await readFile(path.join(root, 'docs/tutorials/aho-corasick-tutorial.md'), 'utf8')).replace(/\r\n/g, '\n');
const digest = value => createHash('sha256').update(value).digest('hex');
const sources = [...markdown.matchAll(/^```mermaid\n([\s\S]*?)^```\s*$/gm)].map(match => match[1].trim());
assert.ok(sources.length > 0, '教程中没有 Mermaid 图');
assert.equal(new Set(sources).size, sources.length, '图示源码重复');
const hashes = sources.map(source => digest(source).slice(0, 16));
const lock = await readFile(path.join(here, 'package-lock.json'));
const generator = digest(Buffer.concat([await readFile(fileURLToPath(import.meta.url)), lock]));

async function verify() {
  const manifest = JSON.parse(await readFile(path.join(output, 'manifest.json'), 'utf8'));
  assert.equal(manifest.generator, generator, '图示生成工具或依赖已经改变');
  assert.deepEqual(manifest.sources, hashes, 'Mermaid 源码已改变，需要重新生成图示');
  for (const [file, expected] of Object.entries(manifest.files))
    assert.equal(digest(await readFile(path.join(output, file))), expected, `图示不一致：${file}`);
  assert.equal(Object.keys(manifest.files).length, sources.length * 4);
  console.log(`Mermaid assets verified: ${sources.length} diagrams, light/dark PNG and SVG`);
}

if (process.argv.includes('--check')) {
  await verify();
  process.exit(0);
}

if (process.argv.includes('--ensure')) {
  try {
    await verify();
    const now = new Date();
    await utimes(path.join(output, 'manifest.json'), now, now);
    process.exit(0);
  } catch { /* 缓存失效时才运行依赖安装与图示渲染。 */ }
  const dependencyStamp = path.join(here, 'node_modules/.ac-tutorial-dependencies');
  let installed = '';
  try { installed = await readFile(dependencyStamp, 'utf8'); } catch {}
  if (installed !== digest(lock)) {
    execSync('npm ci --ignore-scripts --no-audit --no-fund', { cwd: here, stdio: 'inherit' });
    await writeFile(dependencyStamp, digest(lock));
  }
}

const { build } = await import('esbuild');
const { chromium } = await import('playwright');
const bundle = await build({
  stdin: { contents: "import mermaid from 'mermaid'; window.mermaid = mermaid;", resolveDir: here },
  bundle: true, write: false, format: 'iife', platform: 'browser', target: 'es2022', minify: true
});
await mkdir(output, { recursive: true });
// 使用已安装的 Edge，不需要为运行插件安装浏览器；只有修改图示的开发者需要生成工具。
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const files = {};
try {
  const context = await browser.newContext({ viewport: { width: 1800, height: 1200 }, deviceScaleFactor: 2 });
  await context.route(/^https?:/, route => route.abort());
  const page = await context.newPage();
  await page.setContent('<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"></head><body style="margin:0"><div id="diagram" style="display:inline-block;padding:16px"></div></body></html>');
  await page.addScriptTag({ content: bundle.outputFiles[0].text });
  for (const theme of ['light', 'dark']) {
    for (let index = 0; index < sources.length; index++) {
      const result = await page.evaluate(async ({ source, theme, index }) => {
        const dark = theme === 'dark';
        const background = dark ? '#2D3A2F' : '#F0F5E9';
        window.mermaid.initialize({
          startOnLoad: false, securityLevel: 'strict', theme: 'base',
          fontFamily: 'Microsoft YaHei UI, Segoe UI, sans-serif',
          flowchart: { htmlLabels: false, useMaxWidth: false, curve: 'linear' },
          themeVariables: {
            darkMode: dark, background, fontSize: '17px',
            primaryColor: dark ? '#304A43' : '#DEECE1', primaryTextColor: dark ? '#E3ECE7' : '#243D3D',
            primaryBorderColor: dark ? '#65D7C8' : '#087F78', lineColor: dark ? '#ABBBB2' : '#53665D',
            secondaryColor: background, tertiaryColor: background,
            edgeLabelBackground: background
          }
        });
        const { svg } = await window.mermaid.render(`ac-${theme}-${index}`, source);
        const host = document.getElementById('diagram');
        host.style.background = background;
        host.innerHTML = svg;
        await document.fonts.ready;
        const element = host.querySelector('svg');
        const box = element.getBoundingClientRect();
        return { svg, width: box.width, height: box.height };
      }, { source: sources[index], theme, index });
      assert.ok(result.width > 100 && result.height > 50, 'Mermaid 生成了空图');
      const name = `${hashes[index]}.${theme}`;
      const png = await page.locator('#diagram').screenshot();
      await writeFile(path.join(output, name + '.png'), png);
      await writeFile(path.join(output, name + '.svg'), result.svg);
      files[name + '.png'] = digest(png);
      files[name + '.svg'] = digest(result.svg);
      console.log(`${index + 1}/${sources.length} ${theme}: ${Math.round(result.width)} × ${Math.round(result.height)}`);
    }
  }
} finally {
  await browser.close();
}
await writeFile(path.join(output, 'manifest.json'), JSON.stringify({ generator, mermaid: '11.17.2', scale: 2, sources: hashes, files }, null, 2) + '\n');
console.log(`Generated ${sources.length} offline Mermaid diagrams`);
