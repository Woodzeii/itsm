using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "agent_groups",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_classes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_classes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_exceptions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    exception_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_work_day = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "criticality_levels",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_criticality_levels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dictionaries",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionaries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_policies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    min_length = table.Column<int>(type: "integer", nullable: false, defaultValue: 8),
                    require_digit = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    require_uppercase = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    require_lowercase = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    require_special = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    expiration_days = table.Column<int>(type: "integer", nullable: true),
                    max_failed_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    lockout_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_catalog",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_catalog", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_catalog_service_catalog_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "public",
                        principalTable: "service_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_policies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    schedule_type = table.Column<string>(type: "text", nullable: true),
                    reaction_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    resolution_time_minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_roles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_statuses",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_sla_pausing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_types",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_built_in = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_portal_available = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "working_schedules",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    schedule_type = table.Column<string>(type: "text", nullable: false, defaultValue: "24x7"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_working_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_class_attributes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_class_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    data_type = table.Column<string>(type: "text", nullable: false, defaultValue: "string"),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_value = table.Column<string>(type: "text", nullable: true),
                    options = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_class_attributes", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_class_attributes_asset_classes_asset_class_id",
                        column: x => x.asset_class_id,
                        principalSchema: "public",
                        principalTable: "asset_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dictionary_values",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dictionary_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_dictionary_values_dictionaries_dictionary_id",
                        column: x => x.dictionary_id,
                        principalSchema: "public",
                        principalTable: "dictionaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_base_articles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_base_articles", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_base_articles_service_catalog_service_id",
                        column: x => x.service_id,
                        principalSchema: "public",
                        principalTable: "service_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    object_sid = table.Column<string>(type: "text", nullable: true),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    department = table.Column<string>(type: "text", nullable: true),
                    manager_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    two_fa_secret = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Unverified"),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    verification_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    verification_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<int>(type: "integer", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_manager = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_users_manager_id",
                        column: x => x.manager_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_status_transitions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_status_id = table.Column<int>(type: "integer", nullable: false),
                    to_status_id = table.Column<int>(type: "integer", nullable: false),
                    required_role_code = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_status_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_status_transitions_ticket_statuses_from_status_id",
                        column: x => x.from_status_id,
                        principalSchema: "public",
                        principalTable: "ticket_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_status_transitions_ticket_statuses_to_status_id",
                        column: x => x.to_status_id,
                        principalSchema: "public",
                        principalTable: "ticket_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "escalation_rules",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_type_id = table.Column<int>(type: "integer", nullable: false),
                    on_sla_breach = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    on_manual = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_escalation_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_escalation_rules_ticket_types_ticket_type_id",
                        column: x => x.ticket_type_id,
                        principalSchema: "public",
                        principalTable: "ticket_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_templates",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    ticket_type_id = table.Column<int>(type: "integer", nullable: true),
                    fields_schema = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    service_catalog_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_templates_service_catalog_service_catalog_id",
                        column: x => x.service_catalog_id,
                        principalSchema: "public",
                        principalTable: "service_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_form_templates_service_catalog_service_id",
                        column: x => x.service_id,
                        principalSchema: "public",
                        principalTable: "service_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_form_templates_ticket_types_ticket_type_id",
                        column: x => x.ticket_type_id,
                        principalSchema: "public",
                        principalTable: "ticket_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_values",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_type_id = table.Column<int>(type: "integer", nullable: false),
                    criticality_level_id = table.Column<int>(type: "integer", nullable: false),
                    reaction_time_minutes = table.Column<int>(type: "integer", nullable: true),
                    resolution_time_minutes = table.Column<int>(type: "integer", nullable: true),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_sla_values_criticality_levels_criticality_level_id",
                        column: x => x.criticality_level_id,
                        principalSchema: "public",
                        principalTable: "criticality_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sla_values_ticket_types_ticket_type_id",
                        column: x => x.ticket_type_id,
                        principalSchema: "public",
                        principalTable: "ticket_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "working_hours",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    working_schedule_id = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_working_hours", x => x.id);
                    table.ForeignKey(
                        name: "fk_working_hours_working_schedules_working_schedule_id",
                        column: x => x.working_schedule_id,
                        principalSchema: "public",
                        principalTable: "working_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inventory_number = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    asset_class_id = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "text", nullable: true),
                    serial_number = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    lifecycle_stage = table.Column<string>(type: "text", nullable: false, defaultValue: "Purchased"),
                    license_expiration_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_user_id = table.Column<int>(type: "integer", nullable: true),
                    warehouse_id = table.Column<int>(type: "integer", nullable: true),
                    location_id = table.Column<int>(type: "integer", nullable: true),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_assets_asset_classes_asset_class_id",
                        column: x => x.asset_class_id,
                        principalSchema: "public",
                        principalTable: "asset_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assets_users_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mode = table.Column<string>(type: "text", nullable: false, defaultValue: "auto"),
                    manual_assigner_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignment_settings_users_manual_assigner_user_id",
                        column: x => x.manual_assigner_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    old_values = table.Column<string>(type: "text", nullable: true),
                    new_values = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "login_audits",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    username = table.Column<string>(type: "text", nullable: false),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_audits", x => x.id);
                    table.ForeignKey(
                        name: "fk_login_audits_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_role_mappings",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role_mappings", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_role_mappings_system_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "public",
                        principalTable: "system_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_role_mappings_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_fields",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    form_template_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    field_type = table.Column<string>(type: "text", nullable: false, defaultValue: "string"),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_value = table.Column<string>(type: "text", nullable: true),
                    validation_rules = table.Column<string>(type: "text", nullable: true),
                    hint = table.Column<string>(type: "text", nullable: true),
                    is_built_in = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_engineer_only = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    dictionary_id = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_fields", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_fields_dictionaries_dictionary_id",
                        column: x => x.dictionary_id,
                        principalSchema: "public",
                        principalTable: "dictionaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_form_fields_form_templates_form_template_id",
                        column: x => x.form_template_id,
                        principalSchema: "public",
                        principalTable: "form_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_history_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    action_type = table.Column<string>(type: "text", nullable: true),
                    old_status = table.Column<string>(type: "text", nullable: true),
                    new_status = table.Column<string>(type: "text", nullable: true),
                    old_assigned_user_id = table.Column<int>(type: "integer", nullable: true),
                    new_assigned_user_id = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_history_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_history_logs_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_history_logs_users_new_assigned_user_id",
                        column: x => x.new_assigned_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_history_logs_users_old_assigned_user_id",
                        column: x => x.old_assigned_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_history_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_movements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_id = table.Column<int>(type: "integer", nullable: false),
                    movement_type = table.Column<string>(type: "text", nullable: false),
                    from_user_id = table.Column<int>(type: "integer", nullable: true),
                    to_user_id = table.Column<int>(type: "integer", nullable: true),
                    from_warehouse_id = table.Column<int>(type: "integer", nullable: true),
                    to_warehouse_id = table.Column<int>(type: "integer", nullable: true),
                    from_location_id = table.Column<int>(type: "integer", nullable: true),
                    to_location_id = table.Column<int>(type: "integer", nullable: true),
                    from_department_id = table.Column<int>(type: "integer", nullable: true),
                    to_department_id = table.Column<int>(type: "integer", nullable: true),
                    performed_by_id = table.Column<int>(type: "integer", nullable: false),
                    performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_movements_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_movements_users_from_user_id",
                        column: x => x.from_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_movements_users_performed_by_id",
                        column: x => x.performed_by_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_movements_users_to_user_id",
                        column: x => x.to_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: true),
                    criticality_level_id = table.Column<int>(type: "integer", nullable: true),
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    creator_id = table.Column<int>(type: "integer", nullable: false),
                    assignee_id = table.Column<int>(type: "integer", nullable: true),
                    assignee_group_id = table.Column<int>(type: "integer", nullable: true),
                    asset_id = table.Column<int>(type: "integer", nullable: true),
                    custom_fields_data = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    sla_policy_id = table.Column<int>(type: "integer", nullable: true),
                    sla_reaction_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_resolution_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_reaction_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_resolution_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_reaction_escalated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_resolution_escalated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_sla_paused = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    actual_reaction_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_resolution_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_timer_paused = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sla_accumulated_pause_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    escalation_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    target_system = table.Column<string>(type: "text", nullable: true),
                    planned_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    planned_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    service_catalog_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_tickets_agent_groups_assignee_group_id",
                        column: x => x.assignee_group_id,
                        principalSchema: "public",
                        principalTable: "agent_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_criticality_levels_criticality_level_id",
                        column: x => x.criticality_level_id,
                        principalSchema: "public",
                        principalTable: "criticality_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_service_catalog_service_catalog_id",
                        column: x => x.service_catalog_id,
                        principalSchema: "public",
                        principalTable: "service_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_service_catalog_service_id",
                        column: x => x.service_id,
                        principalSchema: "public",
                        principalTable: "service_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_sla_policies_sla_policy_id",
                        column: x => x.sla_policy_id,
                        principalSchema: "public",
                        principalTable: "sla_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_ticket_statuses_status_id",
                        column: x => x.status_id,
                        principalSchema: "public",
                        principalTable: "ticket_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_ticket_types_type_id",
                        column: x => x.type_id,
                        principalSchema: "public",
                        principalTable: "ticket_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_users_assignee_id",
                        column: x => x.assignee_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_users_creator_id",
                        column: x => x.creator_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "form_field_visibilities",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    form_field_id = table.Column<int>(type: "integer", nullable: false),
                    role_code = table.Column<string>(type: "text", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_editable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_field_visibilities", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_field_visibilities_form_fields_form_field_id",
                        column: x => x.form_field_id,
                        principalSchema: "public",
                        principalTable: "form_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: true),
                    ticket_id = table.Column<int>(type: "integer", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_ticket_mappings",
                schema: "public",
                columns: table => new
                {
                    release_ticket_id = table.Column<int>(type: "integer", nullable: false),
                    task_ticket_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_release_ticket_mappings", x => new { x.release_ticket_id, x.task_ticket_id });
                    table.ForeignKey(
                        name: "fk_release_ticket_mappings_tickets_release_ticket_id",
                        column: x => x.release_ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_release_ticket_mappings_tickets_task_ticket_id",
                        column: x => x.task_ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_approvals",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    approver_id = table.Column<int>(type: "integer", nullable: false),
                    step_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: true, defaultValue: "Pending"),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_approvals", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_approvals_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_approvals_users_approver_id",
                        column: x => x.approver_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_asset_mappings",
                schema: "public",
                columns: table => new
                {
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    asset_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_asset_mappings", x => new { x.ticket_id, x.asset_id });
                    table.ForeignKey(
                        name: "fk_ticket_asset_mappings_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_asset_mappings_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_audit_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    field_name = table.Column<string>(type: "text", nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_audit_logs_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_escalations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    initiator_user_id = table.Column<int>(type: "integer", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    from_criticality_level_id = table.Column<int>(type: "integer", nullable: true),
                    to_criticality_level_id = table.Column<int>(type: "integer", nullable: true),
                    notified_user_ids = table.Column<string>(type: "text", nullable: true),
                    escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_escalations", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_escalations_criticality_levels_from_criticality_leve",
                        column: x => x.from_criticality_level_id,
                        principalSchema: "public",
                        principalTable: "criticality_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_escalations_criticality_levels_to_criticality_level_",
                        column: x => x.to_criticality_level_id,
                        principalSchema: "public",
                        principalTable: "criticality_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_escalations_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_escalations_users_initiator_user_id",
                        column: x => x.initiator_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_messages",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    message_body = table.Column<string>(type: "text", nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    source_type = table.Column<string>(type: "text", nullable: true, defaultValue: "web"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_messages_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_messages_users_author_id",
                        column: x => x.author_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_message_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_attachments_ticket_messages_ticket_message_id",
                        column: x => x.ticket_message_id,
                        principalSchema: "public",
                        principalTable: "ticket_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "ix_agent_groups_code",
                schema: "public",
                table: "agent_groups",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_class_attributes_asset_class_id_code",
                schema: "public",
                table: "asset_class_attributes",
                columns: new[] { "asset_class_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_classes_code",
                schema: "public",
                table: "asset_classes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_history_logs_asset_id",
                schema: "public",
                table: "asset_history_logs",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_history_logs_new_assigned_user_id",
                schema: "public",
                table: "asset_history_logs",
                column: "new_assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_history_logs_old_assigned_user_id",
                schema: "public",
                table: "asset_history_logs",
                column: "old_assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_history_logs_user_id",
                schema: "public",
                table: "asset_history_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_asset_id",
                schema: "public",
                table: "asset_movements",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_from_user_id",
                schema: "public",
                table: "asset_movements",
                column: "from_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_performed_at",
                schema: "public",
                table: "asset_movements",
                column: "performed_at");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_performed_by_id",
                schema: "public",
                table: "asset_movements",
                column: "performed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_to_user_id",
                schema: "public",
                table: "asset_movements",
                column: "to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_asset_class_id",
                schema: "public",
                table: "assets",
                column: "asset_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_assigned_user_id",
                schema: "public",
                table: "assets",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_inventory_number",
                schema: "public",
                table: "assets",
                column: "inventory_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assets_lifecycle_stage",
                schema: "public",
                table: "assets",
                column: "lifecycle_stage");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_settings_manual_assigner_user_id",
                schema: "public",
                table: "assignment_settings",
                column: "manual_assigner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_ticket_message_id",
                schema: "public",
                table: "attachments",
                column: "ticket_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_uploaded_by_id",
                schema: "public",
                table: "attachments",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at",
                schema: "public",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                schema: "public",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_exceptions_exception_date",
                schema: "public",
                table: "calendar_exceptions",
                column: "exception_date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_criticality_levels_code",
                schema: "public",
                table: "criticality_levels",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dictionaries_code",
                schema: "public",
                table: "dictionaries",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_values_dictionary_id_code",
                schema: "public",
                table: "dictionary_values",
                columns: new[] { "dictionary_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_escalation_rules_ticket_type_id",
                schema: "public",
                table: "escalation_rules",
                column: "ticket_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_field_visibilities_form_field_id_role_code",
                schema: "public",
                table: "form_field_visibilities",
                columns: new[] { "form_field_id", "role_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_form_fields_dictionary_id",
                schema: "public",
                table: "form_fields",
                column: "dictionary_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_fields_form_template_id_code",
                schema: "public",
                table: "form_fields",
                columns: new[] { "form_template_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_form_templates_service_catalog_id",
                schema: "public",
                table: "form_templates",
                column: "service_catalog_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_templates_service_id",
                schema: "public",
                table: "form_templates",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_templates_ticket_type_id",
                schema: "public",
                table: "form_templates",
                column: "ticket_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_articles_service_id",
                schema: "public",
                table: "knowledge_base_articles",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_login_audits_attempted_at",
                schema: "public",
                table: "login_audits",
                column: "attempted_at");

            migrationBuilder.CreateIndex(
                name: "ix_login_audits_user_id",
                schema: "public",
                table: "login_audits",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_login_audits_username",
                schema: "public",
                table: "login_audits",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_created_at",
                schema: "public",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_ticket_id",
                schema: "public",
                table: "notifications",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_is_read",
                schema: "public",
                table: "notifications",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_release_ticket_mappings_task_ticket_id",
                schema: "public",
                table: "release_ticket_mappings",
                column: "task_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_catalog_parent_id",
                schema: "public",
                table: "service_catalog",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_values_criticality_level_id",
                schema: "public",
                table: "sla_values",
                column: "criticality_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_values_ticket_type_id_criticality_level_id",
                schema: "public",
                table: "sla_values",
                columns: new[] { "ticket_type_id", "criticality_level_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_system_roles_code",
                schema: "public",
                table: "system_roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_code",
                schema: "public",
                table: "tenants",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_approvals_approver_id",
                schema: "public",
                table: "ticket_approvals",
                column: "approver_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_approvals_ticket_id_step_number",
                schema: "public",
                table: "ticket_approvals",
                columns: new[] { "ticket_id", "step_number" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_asset_mappings_asset_id",
                schema: "public",
                table: "ticket_asset_mappings",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_audit_logs_ticket_id",
                schema: "public",
                table: "ticket_audit_logs",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_audit_logs_user_id",
                schema: "public",
                table: "ticket_audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_escalations_escalated_at",
                schema: "public",
                table: "ticket_escalations",
                column: "escalated_at");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_escalations_from_criticality_level_id",
                schema: "public",
                table: "ticket_escalations",
                column: "from_criticality_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_escalations_initiator_user_id",
                schema: "public",
                table: "ticket_escalations",
                column: "initiator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_escalations_ticket_id",
                schema: "public",
                table: "ticket_escalations",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_escalations_to_criticality_level_id",
                schema: "public",
                table: "ticket_escalations",
                column: "to_criticality_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_messages_author_id",
                schema: "public",
                table: "ticket_messages",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_messages_ticket_id",
                schema: "public",
                table: "ticket_messages",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_status_transitions_from_status_id_to_status_id",
                schema: "public",
                table: "ticket_status_transitions",
                columns: new[] { "from_status_id", "to_status_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_status_transitions_to_status_id",
                schema: "public",
                table: "ticket_status_transitions",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_statuses_code",
                schema: "public",
                table: "ticket_statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_types_code",
                schema: "public",
                table: "ticket_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_asset_id",
                schema: "public",
                table: "tickets",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_assignee_group_id",
                schema: "public",
                table: "tickets",
                column: "assignee_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_assignee_id",
                schema: "public",
                table: "tickets",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_creator_id",
                schema: "public",
                table: "tickets",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_criticality_level_id",
                schema: "public",
                table: "tickets",
                column: "criticality_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_service_catalog_id",
                schema: "public",
                table: "tickets",
                column: "service_catalog_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_service_id",
                schema: "public",
                table: "tickets",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_sla_policy_id",
                schema: "public",
                table: "tickets",
                column: "sla_policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_status_id",
                schema: "public",
                table: "tickets",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_type_id",
                schema: "public",
                table: "tickets",
                column: "type_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_mappings_role_id",
                schema: "public",
                table: "user_role_mappings",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "public",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_manager_id",
                schema: "public",
                table: "users",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_object_sid",
                schema: "public",
                table: "users",
                column: "object_sid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_id",
                schema: "public",
                table: "users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                schema: "public",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_verification_token_hash",
                schema: "public",
                table: "users",
                column: "verification_token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_working_hours_working_schedule_id_day_of_week",
                schema: "public",
                table: "working_hours",
                columns: new[] { "working_schedule_id", "day_of_week" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_class_attributes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "asset_history_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "asset_movements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "assignment_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "attachments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "calendar_exceptions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "dictionary_values",
                schema: "public");

            migrationBuilder.DropTable(
                name: "escalation_rules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "form_field_visibilities",
                schema: "public");

            migrationBuilder.DropTable(
                name: "knowledge_base_articles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "login_audits",
                schema: "public");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "public");

            migrationBuilder.DropTable(
                name: "password_policies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "release_ticket_mappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sla_values",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_approvals",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_asset_mappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_audit_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_escalations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_status_transitions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_role_mappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "working_hours",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_messages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "form_fields",
                schema: "public");

            migrationBuilder.DropTable(
                name: "system_roles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "working_schedules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "dictionaries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "form_templates",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agent_groups",
                schema: "public");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "criticality_levels",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sla_policies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_statuses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "service_catalog",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_types",
                schema: "public");

            migrationBuilder.DropTable(
                name: "asset_classes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "users",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "public");
        }
    }
}
