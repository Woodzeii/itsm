# OpenAPI Type Generation

The TypeScript client and types are generated from the repository-root
[`API_CONTRACT.json`](../API_CONTRACT.json). Generation does not require a
running backend, so frontend work can proceed before backend mocks are available.

From the `frontend` directory, run:

```powershell
npm run generate:types
```

To regenerate and check for uncommitted generated-client differences:

```powershell
npm run generate:types:check
```

Some API operations in the contract do not declare response schemas. The
workspace adapter uses its UI-facing models for those response bodies. UI
actions with no matching contract operation report an explicit unsupported
operation error rather than calling an assumed route.

В целом ветка дополняет моки, насколько я заметил, но я не успел её протестировать. Если можете- проверьте изменения агентом.