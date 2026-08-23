import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

// https://vite.dev/config/
export default defineConfig({
  // Relative base so the built app works when served from any sub-path
  // behind a reverse proxy (e.g. https://public-address.example.com/scores/).
  base: './',
  plugins: [svelte()],
})
