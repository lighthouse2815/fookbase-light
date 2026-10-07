# React + TypeScript + Vite

Các file trong `tests/` chỉ có ở máy local và được Git bỏ qua.
Lệnh test và hướng dẫn kiểm tra bằng trình duyệt bên dưới cần bộ test local.

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

## API during development

`npm run dev` proxies `/api` requests to `http://localhost:5000`, the default backend URL.
Set `VITE_API_PROXY_TARGET` to use another backend URL. For a separately deployed frontend,
set `VITE_API_BASE_URL` to the API origin instead.

The sign-in flow stores the JWT session in local storage and refreshes an expired access token automatically.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend enabling type-aware lint rules by installing `oxlint-tsgolint` and editing `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

See the [Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules) for the full list of rules and categories.
