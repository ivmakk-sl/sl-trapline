// Builds the two page scripts, each one IIFE file, not minified, that C# embeds and sends to the root page:
// `--mode page` (the HUD trap list) and `--mode trappanel` (the trap window). Vite's library build takes one entry, so
// package.json runs it once for each mode. Each entry sets its window global itself, so the bundles export nothing.
// Vitest (mode `test`) runs the page tests against those bundles and the game's own web pages.
import { defineConfig } from 'vitest/config';

const ENTRIES: Record<string, { entry: string; name: string }> = {
  page: { entry: 'src/Web/page/main.ts', name: 'traplinePage' },
  trappanel: { entry: 'src/Web/trappanel/main.ts', name: 'traplineTrapPanel' },
};

export default defineConfig(({ mode }) => {
  if (mode === 'test') {
    return {
      test: {
        include: ['tests/page/**/*.test.ts'],
        // Each test loads a game web page in jsdom, which takes about half a second.
        testTimeout: 20000,
      },
    };
  }
  const target = ENTRIES[mode];
  if (!target) throw new Error(`vite.config.ts: unknown mode "${mode}", use page or trappanel`);
  return {
    build: {
      lib: {
        entry: target.entry,
        formats: ['iife'],
        name: target.name,
        fileName: () => `${mode}.js`,
      },
      outDir: 'obj/page',
      // The page build runs first and clears the folder; the trappanel build adds its file.
      emptyOutDir: mode === 'page',
      minify: false,
    },
  };
});
