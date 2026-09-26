using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                name: "ticket_statuses",
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
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_types", x => x.id);
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_users_manager_id",
                        column: x => x.manager_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "form_templates",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    fields_schema = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_templates_service_catalog_service_id",
                        column: x => x.service_id,
                        principalSchema: "public",
                        principalTable: "service_catalog",
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
                name: "assets",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inventory_number = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: true),
                    serial_number = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    license_expiration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    assigned_user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_assets_users_assigned_user_id",
                        column: x => x.assigned_user_id,
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
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    creator_id = table.Column<int>(type: "integer", nullable: false),
                    assignee_id = table.Column<int>(type: "integer", nullable: true),
                    assignee_group_id = table.Column<int>(type: "integer", nullable: true),
                    custom_fields_data = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    sla_policy_id = table.Column<int>(type: "integer", nullable: true),
                    sla_reaction_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_resolution_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_reaction_escalated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_resolution_escalated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    actual_reaction_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_resolution_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_timer_paused = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sla_accumulated_pause_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    target_system = table.Column<string>(type: "text", nullable: true),
                    planned_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    planned_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    service_catalog_id = table.Column<int>(type: "integer", nullable: true),
                    sla_policy_id1 = table.Column<int>(type: "integer", nullable: true),
                    ticket_status_id = table.Column<int>(type: "integer", nullable: true),
                    ticket_type_id = table.Column<int>(type: "integer", nullable: true)
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
                        name: "fk_tickets_sla_policies_sla_policy_id1",
                        column: x => x.sla_policy_id1,
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
                        name: "fk_tickets_ticket_statuses_ticket_status_id",
                        column: x => x.ticket_status_id,
                        principalSchema: "public",
                        principalTable: "ticket_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_ticket_types_ticket_type_id",
                        column: x => x.ticket_type_id,
                        principalSchema: "public",
                        principalTable: "ticket_types",
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

            migrationBuilder.CreateIndex(
                name: "ix_agent_groups_code",
                schema: "public",
                table: "agent_groups",
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
                name: "ix_calendar_exceptions_exception_date",
                schema: "public",
                table: "calendar_exceptions",
                column: "exception_date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_form_templates_service_id",
                schema: "public",
                table: "form_templates",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_articles_service_id",
                schema: "public",
                table: "knowledge_base_articles",
                column: "service_id");

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
                name: "ix_system_roles_code",
                schema: "public",
                table: "system_roles",
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
                name: "ix_tickets_sla_policy_id1",
                schema: "public",
                table: "tickets",
                column: "sla_policy_id1");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_status_id",
                schema: "public",
                table: "tickets",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_ticket_status_id",
                schema: "public",
                table: "tickets",
                column: "ticket_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_ticket_type_id",
                schema: "public",
                table: "tickets",
                column: "ticket_type_id");

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
                name: "ix_users_username",
                schema: "public",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_history_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "calendar_exceptions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "form_templates",
                schema: "public");

            migrationBuilder.DropTable(
                name: "knowledge_base_articles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "release_ticket_mappings",
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
                name: "ticket_messages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_role_mappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "system_roles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agent_groups",
                schema: "public");

            migrationBuilder.DropTable(
                name: "service_catalog",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sla_policies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_statuses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_types",
                schema: "public");

            migrationBuilder.DropTable(
                name: "users",
                schema: "public");
        }
    }
}
