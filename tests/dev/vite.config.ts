// The dev harness: the game's CoreUI1.html in a normal browser with the page script and a fake trap
// state, for look and layout work outside the game. npm run dev starts it. The in-game check stays the
// way to verify a change.
import fs from 'node:fs';
import path from 'node:path';
import url from 'node:url';
import { defineConfig, type Plugin } from 'vite';

const HERE = path.dirname(url.fileURLToPath(import.meta.url));
const MOD_ROOT = path.resolve(HERE, '..', '..');
const GAME_DIR = process.env.SL_GAME_DIR || 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Survival Log';
const WEB_UI = path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI');

const TYPES: Record<string, string> = {
  '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.webp': 'image/webp', '.svg': 'image/svg+xml',
  '.woff': 'font/woff', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.otf': 'font/otf',
};

// Serves the game's WebUI folder with its folder tree as it is, so CoreUI1.html finds
// ../../vue.runtime.global.prod.js and its other files. A path with no game file goes on to Vite.
function gameWebUi(): Plugin {
  return {
    name: 'trapline-game-webui',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        const urlPath = decodeURIComponent((req.url || '/').split('?')[0]);
        const file = path.join(WEB_UI, urlPath);
        // Only a file inside the WebUI folder: a path that leaves it (..) is not a game page file.
        const relative = path.relative(WEB_UI, file);
        const inside = relative !== '' && !relative.startsWith('..') && !path.isAbsolute(relative);
        if (!inside || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
          next();
          return;
        }
        res.setHeader('Content-Type', TYPES[path.extname(file).toLowerCase()] || 'application/octet-stream');
        fs.createReadStream(file).pipe(res);
      });
    },
  };
}

export default defineConfig({
  root: HERE,
  plugins: [gameWebUi()],
  server: {
    fs: { allow: [MOD_ROOT] },
  },
});
