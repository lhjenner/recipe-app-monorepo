# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

## Shared page layout

Authenticated pages share the `.app-main` and `.app-page` layout in
[app-shell.css](src/styles/app-shell.css). Above 700px, panels fill the available
viewport width with 32px outer gutters. At 700px and below, the existing mobile
layout retains 16px outer gutters. Page-specific styles should not override the
shared panel width.

Browser coverage in [page-layout.spec.ts](tests/e2e/navigation/page-layout.spec.ts)
checks panel bounds and horizontal overflow across desktop and mobile widths,
including both sides of the breakpoint.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## Modal containers

[ModalContainer](src/shared/components/ModalContainer.tsx) is a mount-to-open native
`<dialog>` shell used by the grocery Add/Edit forms. Owners conditionally render
it, supply the accessible heading ID and CSS class, and choose an initial focus
selector (keep it stable while mounted). It adds no content wrappers or styles.
Native `showModal()` provides focus containment and background inertness; cleanup
closes the dialog and restores focus to the connected opener. Escape requests
owner dismissal only when `locked` is false; backdrop clicks do not dismiss.
Grocery owners keep it locked while saving and while an ambiguous result awaits
retry, and continue to disable their own close buttons and draft controls.

[ConfirmDialog](src/shared/components/ConfirmDialog.tsx) intentionally remains
separate: its `open` prop controls visibility, it focuses the non-destructive
Cancel action, and it allows backdrop dismissal. Do not substitute the form
container without accounting for those behavioral differences.

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...

      // Remove tseslint.configs.recommended and replace with this
      tseslint.configs.recommendedTypeChecked,
      // Alternatively, use this for stricter rules
      tseslint.configs.strictTypeChecked,
      // Optionally, add this for stylistic rules
      tseslint.configs.stylisticTypeChecked,

      // Other configs...
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])

```

You can also install [eslint-plugin-react-x](https://npmx.dev/package/eslint-plugin-react-x) and [eslint-plugin-react-dom](https://npmx.dev/package/eslint-plugin-react-dom) for React-specific lint rules:

```js
// eslint.config.js
import reactX from 'eslint-plugin-react-x'
import reactDom from 'eslint-plugin-react-dom'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...
      // Enable lint rules for React
      reactX.configs['recommended-typescript'],
      // Enable lint rules for React DOM
      reactDom.configs.recommended,
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])

```
