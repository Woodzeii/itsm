# ITSM Test Runner

Консольное приложение для запуска тестов из корня репозитория:

```powershell
dotnet run --project scripts\TestRunner\TestRunner.csproj
```

В меню доступны тесты проекта `backend\itsm.Tests` и JavaScript-тесты верхнего уровня папки `scripts`. Для запуска скриптов требуется установленный [k6](https://grafana.com/docs/k6/latest/set-up/install-k6/).

Для скрипта, в имени которого есть `load`, приложение запрашивает адрес API, число виртуальных пользователей, длительность, порог ответа, необязательный ID справочника и JWT-токен. Если токен не введён, скрипт использует `LOAD_TEST_JWT_TOKEN` из `backend\.env`. Перед запуском теста настройте API и базу данных.
