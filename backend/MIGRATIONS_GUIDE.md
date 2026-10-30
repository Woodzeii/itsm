# Миграции для поддержки мультитенантности (НФТ-02)

## Описание

Данный файл содержит примеры миграций Entity Framework Core для добавления поддержки `TenantId` во все мультитенантные таблицы.

## Создание миграции

```bash
cd backend
dotnet ef migrations add AddTenantIdToMultiTenantEntities --context ItsmDbContext
```

## Пример миграции (автоматически сгенерированная)

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations;

public partial class AddTenantIdToMultiTenantEntities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Добавляем TenantId колонки со значением по умолчанию 1
        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "tickets",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "service_catalog",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "assets",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "agent_groups",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "ticket_types",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "ticket_statuses",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "criticality_levels",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            table: "form_templates",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        // === ИНДЕКСЫ НА TENANTID ===
        
        // Простые индексы для быстрого фильтрования
        migrationBuilder.CreateIndex(
            name: "ix_users_tenant_id",
            table: "users",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_tickets_tenant_id",
            table: "tickets",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_assets_tenant_id",
            table: "assets",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_agent_groups_tenant_id",
            table: "agent_groups",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_service_catalog_tenant_id",
            table: "service_catalog",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_types_tenant_id",
            table: "ticket_types",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_statuses_tenant_id",
            table: "ticket_statuses",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_criticality_levels_tenant_id",
            table: "criticality_levels",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_form_templates_tenant_id",
            table: "form_templates",
            column: "tenant_id");

        // === СОСТАВНЫЕ ИНДЕКСЫ ДЛЯ УНИКАЛЬНОСТИ В ПРЕДЕЛАХ ТЕНАНТА ===

        // Обновляем уникальные индексы, добавляя TenantId
        // Сначала удаляем старые индексы, если они есть
        migrationBuilder.DropIndex(name: "ix_users_object_sid", table: "users");
        migrationBuilder.DropIndex(name: "ix_users_username", table: "users");
        migrationBuilder.DropIndex(name: "ix_users_email", table: "users");
        migrationBuilder.DropIndex(name: "ix_agent_groups_code", table: "agent_groups");
        migrationBuilder.DropIndex(name: "ix_ticket_types_code", table: "ticket_types");
        migrationBuilder.DropIndex(name: "ix_ticket_statuses_code", table: "ticket_statuses");
        migrationBuilder.DropIndex(name: "ix_criticality_levels_code", table: "criticality_levels");
        migrationBuilder.DropIndex(name: "ix_assets_inventory_number", table: "assets");

        // Создаём новые составные индексы с TenantId
        migrationBuilder.CreateIndex(
            name: "ix_users_tenant_id_object_sid",
            table: "users",
            columns: new[] { "tenant_id", "object_sid" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_tenant_id_username",
            table: "users",
            columns: new[] { "tenant_id", "username" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_tenant_id_email",
            table: "users",
            columns: new[] { "tenant_id", "email" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_agent_groups_tenant_id_code",
            table: "agent_groups",
            columns: new[] { "tenant_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_ticket_types_tenant_id_code",
            table: "ticket_types",
            columns: new[] { "tenant_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_ticket_statuses_tenant_id_code",
            table: "ticket_statuses",
            columns: new[] { "tenant_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_criticality_levels_tenant_id_code",
            table: "criticality_levels",
            columns: new[] { "tenant_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_assets_tenant_id_inventory_number",
            table: "assets",
            columns: new[] { "tenant_id", "inventory_number" },
            unique: true);

        // === ДОБАВЛЯЕМ ВНЕШНИЕ КЛЮЧИ ===
        
        // ForeignKey для users -> tenants
        migrationBuilder.AddForeignKey(
            name: "fk_users_tenant_id",
            table: "users",
            column: "tenant_id",
            principalTable: "tenants",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        // Примечание: для связей между мультитенантными таблицами
        // (например, ticket.creator_id -> user.id) фильтр автоматически
        // обеспечит, что пользователь принадлежит тому же тенанту
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // === УДАЛЯЕМ ВНЕШНИЕ КЛЮЧИ ===
        migrationBuilder.DropForeignKey(
            name: "fk_users_tenant_id",
            table: "users");

        // === УДАЛЯЕМ ИНДЕКСЫ ===
        migrationBuilder.DropIndex(name: "ix_users_tenant_id", table: "users");
        migrationBuilder.DropIndex(name: "ix_users_tenant_id_object_sid", table: "users");
        migrationBuilder.DropIndex(name: "ix_users_tenant_id_username", table: "users");
        migrationBuilder.DropIndex(name: "ix_users_tenant_id_email", table: "users");
        
        // ... (остальные индексы)

        // Восстанавливаем старые индексы
        migrationBuilder.CreateIndex(
            name: "ix_users_object_sid",
            table: "users",
            column: "object_sid",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_username",
            table: "users",
            column: "username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_email",
            table: "users",
            column: "email",
            unique: true);

        // === УДАЛЯЕМ КОЛОНКИ TENANTID ===
        migrationBuilder.DropColumn(name: "tenant_id", table: "users");
        migrationBuilder.DropColumn(name: "tenant_id", table: "tickets");
        migrationBuilder.DropColumn(name: "tenant_id", table: "service_catalog");
        migrationBuilder.DropColumn(name: "tenant_id", table: "assets");
        migrationBuilder.DropColumn(name: "tenant_id", table: "agent_groups");
        migrationBuilder.DropColumn(name: "tenant_id", table: "ticket_types");
        migrationBuilder.DropColumn(name: "tenant_id", table: "ticket_statuses");
        migrationBuilder.DropColumn(name: "tenant_id", table: "criticality_levels");
        migrationBuilder.DropColumn(name: "tenant_id", table: "form_templates");
    }
}
```

## Применение миграции

```bash
# Применить все pending миграции
dotnet ef database update

# Или откатить последнюю миграцию
dotnet ef database update <previous_migration_name>

# Просмотреть список всех миграций
dotnet ef migrations list
```

## Проверка в базе данных

После применения миграции проверьте структуру таблиц:

```sql
-- Проверьте наличие колонки tenant_id
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = 'users'
ORDER BY ordinal_position;

-- Проверьте индексы
SELECT tablename, indexname, indexdef
FROM pg_indexes
WHERE tablename IN ('users', 'tickets', 'assets', 'agent_groups');

-- Проверьте данные
SELECT COUNT(*) as total, COUNT(DISTINCT tenant_id) as tenant_count
FROM users;
```

## Важные замечания

⚠️ **Значение по умолчанию** - все существующие записи получат `tenant_id = 1`

⚠️ **Уникальность** - уникальные индексы теперь включают `tenant_id`, поэтому в разных тенантах могут быть записи с одинаковыми значениями `username`, `code` и т.д.

⚠️ **Связи между таблицами** - при наличии фoreignKey между двумя мультитенантными таблицами, рекомендуется добавить составной индекс на обе стороны

⚠️ **Перформанс** - для больших таблиц миграция может занять время, рекомендуется выполнять вне пиковых часов

## Откат миграции (если что-то пошло не так)

```bash
# Откатить последнюю миграцию (вернуться к предыдущему состоянию)
dotnet ef database update <previous_migration>

# Удалить миграцию из истории
dotnet ef migrations remove

# Пересоздать миграцию
dotnet ef migrations add AddTenantIdToMultiTenantEntities
```
