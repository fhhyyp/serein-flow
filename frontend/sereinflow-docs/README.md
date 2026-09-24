# SereinFlow documentation site

This Vue 3 and TypeScript based VitePress project maintains its own articles in `content/`. The website does not import Markdown from the repository's older `docs/` directory.

From this directory:

```powershell
npm ci
npm run dev
```

Build static output with `npm run build`. The deployable files are written to `.vitepress/dist/`. Edit articles under `content/`; the development server updates them as you save.

Before publishing an API change, check the route and request/response types in `src/SereinFlow.AspNetCore/Controllers/` and `src/SereinFlow.Contracts/`. Keep only current routes in the portal. Do not list compatibility aliases from the singular `/api/library` surface.
