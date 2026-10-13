# SereinFlow documentation site

This Vue 3 and TypeScript based VitePress project maintains its own articles in `content/`. Chinese pages live at the root and matching English pages live under `content/en/`. The website does not import Markdown from the repository's older `docs/` directory.

From this directory:

```powershell
npm ci
npm run dev
```

Build static output with `npm run build`. The deployable files are written to `.vitepress/dist/`. Edit articles under `content/`; the development server updates them as you save.

Use `sereinlang` (or its `sereinscript` alias) for SereinScript code fences. The portal registers its TextMate grammar in `.vitepress/sereinlang.tmLanguage.json`; keep the highlighting rules aligned with the embedded language lexer. Use `text` for execution diagrams and other plain text.

The portal uses the browser's primary language on the first visit: Chinese browsers stay on the root pages, and other languages open the matching `/en/` page. The language switch in the navigation remembers an explicit choice in local storage. Keep Chinese and English page paths aligned so the switch opens the same article. The navigation and home page link to the [GitHub repository](https://github.com/fhhyyp/serein-flow).

Before publishing an API change, check the route and request/response types in `src/SereinFlow.AspNetCore/Controllers/` and `src/SereinFlow.Contracts/`. Keep only current routes in the portal. Do not list compatibility aliases from the singular `/api/library` surface.
