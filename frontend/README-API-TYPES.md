# Генерация TypeScript-типов из OpenAPI

Типы генерируются напрямую из **живого OpenAPI backend**
(`http://localhost:5062/openapi/v1.json`), которое также отображает
**Scalar** (`http://localhost:5062/scalar`).

## Зачем

- Единый источник правды для типов между frontend и backend.
- Автоматически ловим рассинхрон: если backend изменил DTO, а фронт — нет,
  проверка `generate:types:check` падает.

## Требования

Backend запущен:
```powershell
docker compose up -d backend