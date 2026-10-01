using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "public",
                table: "assignment_settings",
                columns: new[] { "id", "manual_assigner_user_id", "mode", "updated_at" },
                values: new object[] { 1, null, "auto", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                schema: "public",
                table: "criticality_levels",
                columns: new[] { "id", "code", "created_at", "is_active", "name", "sort_order" },
                values: new object[,]
                {
                    { 1, "low", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Низкий", 1 },
                    { 2, "medium", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Средний", 2 },
                    { 3, "high", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Высокий", 3 }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "dictionaries",
                columns: new[] { "id", "code", "created_at", "is_system", "name" },
                values: new object[,]
                {
                    { 1, "warehouses", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Склады" },
                    { 2, "locations", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Места" },
                    { 3, "departments", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Подразделения" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "password_policies",
                columns: new[] { "id", "expiration_days", "lockout_minutes", "max_failed_attempts", "min_length", "require_digit", "require_lowercase", "updated_at" },
                values: new object[] { 1, null, 15, 5, 8, true, true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                schema: "public",
                table: "system_roles",
                columns: new[] { "id", "code", "name" },
                values: new object[,]
                {
                    { 1, "tenant_admin", "Администратор тенантов" },
                    { 2, "admin", "Администратор" },
                    { 3, "agent", "Инженер ТП" },
                    { 4, "manager", "Руководитель" },
                    { 5, "portal_user", "Пользователь портала" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "tenants",
                columns: new[] { "id", "code", "created_at", "is_active", "name" },
                values: new object[] { 1, "default", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Default Tenant" });

            migrationBuilder.InsertData(
                schema: "public",
                table: "ticket_statuses",
                columns: new[] { "id", "code", "name", "sort_order" },
                values: new object[,]
                {
                    { 1, "new", "Открыта", 1 },
                    { 2, "pending", "Ожидает выполнения", 2 },
                    { 3, "in_progress", "В работе", 3 }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "ticket_statuses",
                columns: new[] { "id", "code", "is_sla_pausing", "name", "sort_order" },
                values: new object[] { 4, "review", true, "Проверка", 4 });

            migrationBuilder.InsertData(
                schema: "public",
                table: "ticket_statuses",
                columns: new[] { "id", "code", "name", "sort_order" },
                values: new object[] { 5, "closed", "Закрыта", 5 });

            migrationBuilder.InsertData(
                schema: "public",
                table: "ticket_types",
                columns: new[] { "id", "code", "is_portal_available", "name" },
                values: new object[,]
                {
                    { 1, "incident", true, "Инцидент" },
                    { 2, "service_request", true, "Запрос на обслуживание" },
                    { 3, "change", true, "Изменение" },
                    { 4, "access", true, "Запрос на доступ" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "ticket_types",
                columns: new[] { "id", "code", "is_built_in", "name" },
                values: new object[] { 5, "repair", true, "Тикет на ремонт" });

            migrationBuilder.InsertData(
                schema: "public",
                table: "working_schedules",
                columns: new[] { "id", "created_at", "is_active", "name", "schedule_type" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "24x7", "24x7" },
                    { 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "8x5 Стандартный", "work_hours" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "dictionary_values",
                columns: new[] { "id", "code", "created_at", "dictionary_id", "name", "sort_order" },
                values: new object[,]
                {
                    { 1, "main", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, "Основной склад", 1 },
                    { 2, "spare", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, "Резервный склад", 2 },
                    { 3, "office_msk", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Москва, офис", 1 },
                    { 4, "office_spb", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "СПб, офис", 2 },
                    { 5, "it", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, "IT", 1 },
                    { 6, "finance", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, "Бухгалтерия", 2 },
                    { 7, "sales", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, "Отдел продаж", 3 }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "ticket_status_transitions",
                columns: new[] { "id", "created_at", "from_status_id", "required_role_code", "to_status_id" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, null, 2 },
                    { 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, null, 3 },
                    { 3, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, null, 4 },
                    { 4, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4, null, 5 },
                    { 5, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4, null, 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "assignment_settings",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "criticality_levels",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "criticality_levels",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "criticality_levels",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionary_values",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionary_values",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionary_values",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionary_values",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionary_values",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionary_values",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionary_values",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "password_policies",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "system_roles",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "system_roles",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "system_roles",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "system_roles",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "system_roles",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "tenants",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "working_schedules",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "working_schedules",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionaries",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionaries",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "dictionaries",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 5);
        }
    }
}
