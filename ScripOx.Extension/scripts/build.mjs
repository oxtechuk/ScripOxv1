import { build } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { copyFileSync, mkdirSync, existsSync, cpSync } from 'fs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, '..');

const sharedConfig = {
  root,
  plugins: [
    react(),
    tailwindcss(),
  ],
  resolve: {
    alias: { '@': resolve(root, 'src') }
  }
};

async function run() {
  console.log('🚀 Starting ScripOx programmatic build pipeline...\n');

  // ── Build 1: Popup & Options ─────────────────────────────
  console.log('📦 Building Popup & Options...');
  await build({
    ...sharedConfig,
    configFile: false,
    build: {
      outDir: resolve(root, 'dist'),
      emptyOutDir: true, // Clean dist folder first
      rollupOptions: {
        input: {
          popup:   resolve(root, 'popup.html'),
          options: resolve(root, 'options.html'),
        },
        output: {
          chunkFileNames:  'assets/[name]-[hash].js',
          assetFileNames:  'assets/[name]-[hash][extname]',
          entryFileNames:  'assets/[name].js',
        }
      }
    }
  });

  // ── Build 2: Content Script (Standalone) ─────────────────
  console.log('\n📦 Building Content Script...');
  await build({
    ...sharedConfig,
    configFile: false,
    build: {
      outDir: resolve(root, 'dist'),
      emptyOutDir: false, // Keep build 1!
      rollupOptions: {
        input: {
          content: resolve(root, 'src/content/index.tsx'),
        },
        output: {
          entryFileNames: 'content.js',
          // Guarantees no chunking
          inlineDynamicImports: true,
        }
      }
    }
  });

  // ── Build 3: Background Worker (Standalone) ──────────────
  console.log('\n📦 Building Background Service Worker...');
  await build({
    ...sharedConfig,
    configFile: false,
    build: {
      outDir: resolve(root, 'dist'),
      emptyOutDir: false, // Keep build 1 & 2!
      rollupOptions: {
        input: {
          background: resolve(root, 'src/background/background.ts'),
        },
        output: {
          entryFileNames: 'background.js',
          inlineDynamicImports: true,
        }
      }
    }
  });

  // ── Copy manifest.json ──────────────────────────────────
  try {
    const destDir = resolve(root, 'dist');
    if (!existsSync(destDir)) mkdirSync(destDir, { recursive: true });
    copyFileSync(resolve(root, 'manifest.json'), resolve(destDir, 'manifest.json'));
    console.log('\n✓ manifest.json successfully copied to dist/!');
  } catch (err) {
    console.error('\n❌ Could not copy manifest.json:', err);
  }

  // ── Sync to root directory so loading root directly also works ────
  try {
    copyFileSync(resolve(root, 'dist/content.js'), resolve(root, 'content.js'));
    copyFileSync(resolve(root, 'dist/background.js'), resolve(root, 'background.js'));
    if (existsSync(resolve(root, 'dist/icons'))) {
      cpSync(resolve(root, 'dist/icons'), resolve(root, 'icons'), { recursive: true, force: true });
    }
    if (existsSync(resolve(root, 'dist/assets'))) {
      cpSync(resolve(root, 'dist/assets'), resolve(root, 'assets'), { recursive: true, force: true });
    }
    console.log('✓ Synced content.js, background.js, icons, and assets to root extension directory!');
  } catch (err) {
    console.error('❌ Could not sync build files to root:', err);
  }

  console.log('\n✅ ScripOx Extension build complete! Both dist/ and root are valid unpacked extensions.');
}

run().catch((err) => {
  console.error('\n❌ Build pipeline failed:', err);
  process.exit(1);
});
