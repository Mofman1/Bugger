const fs = require('node:fs/promises');
const path = require('node:path');
const packageRoot = process.env.ICON_NODE_MODULES || 'C:/Users/Stuart/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const sharp = require(require.resolve('sharp', { paths: [packageRoot] }));
(async () => {
  const folder = process.argv[2];
  for (const file of (await fs.readdir(folder)).filter(n => n.endsWith('.svg'))) {
    for (const size of [16, 20, 24, 32, 40, 48, 64, 128, 256]) {
      await sharp(path.join(folder, file), { density: 384 }).resize(size, size).png().toFile(path.join(folder, file.replace('.svg', `-${size}.png`)));
    }
    console.log('Rendered ' + file);
  }
})().catch(e => { console.error(e); process.exitCode = 1; });
