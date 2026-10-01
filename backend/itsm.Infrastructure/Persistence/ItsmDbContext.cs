using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence;

public class ItsmDbContext(DbContextOptions<ItsmDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SystemRole> SystemRoles => Set<SystemRole>();
    public DbSet<UserRoleMapping> UserRoleMappings => Set<UserRoleMapping>();
    public DbSet<AgentGroup> AgentGroups => Set<AgentGroup>();
    public DbSet<ServiceCatalog> ServiceCatalog => Set<ServiceCatalog>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<TicketStatus> TicketStatuses => Set<TicketStatus>();
    public DbSet<TicketStatusTransition> TicketStatusTransitions => Set<TicketStatusTransition>();
    public DbSet<CriticalityLevel> CriticalityLevels => Set<CriticalityLevel>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<SlaValue> SlaValues => Set<SlaValue>();
    public DbSet<WorkingSchedule> WorkingSchedules => Set<WorkingSchedule>();
    public DbSet<WorkingHour> WorkingHours => Set<WorkingHour>();
    public DbSet<AssignmentSetting> AssignmentSettings => Set<AssignmentSetting>();
    public DbSet<CalendarException> CalendarExceptions => Set<CalendarException>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetClass> AssetClasses => Set<AssetClass>();
    public DbSet<AssetClassAttribute> AssetClassAttributes => Set<AssetClassAttribute>();
    public DbSet<AssetMovement> AssetMovements => Set<AssetMovement>();
    public DbSet<AssetHistoryLog> AssetHistoryLogs => Set<AssetHistoryLog>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketAssetMapping> TicketAssetMappings => Set<TicketAssetMapping>();
    public DbSet<ReleaseTicketMapping> ReleaseTicketMappings => Set<ReleaseTicketMapping>();
    public DbSet<TicketApproval> TicketApprovals => Set<TicketApproval>();
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles => Set<KnowledgeBaseArticle>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAuditLog> TicketAuditLogs => Set<TicketAuditLog>();

    // ТЗ: новые сущности
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Dictionary> Dictionaries => Set<Dictionary>();
    public DbSet<DictionaryValue> DictionaryValues => Set<DictionaryValue>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    public DbSet<PasswordPolicy> PasswordPolicies => Set<PasswordPolicy>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        // --- 1. ПОЛЬЗОВАТЕЛИ И ИЕРАРХИЯ ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ObjectSid).IsUnique();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.Status).HasDefaultValue("Unverified");
            entity.Property(x => x.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(x => x.IsManager).HasDefaultValue(false);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.VerificationTokenHash).HasMaxLength(128);
            entity.HasIndex(x => x.VerificationTokenHash);

            entity.HasOne(x => x.Manager)
                  .WithMany(x => x.DirectReports)
                  .HasForeignKey(x => x.ManagerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Tenant)
                  .WithMany(t => t.Users)
                  .HasForeignKey(x => x.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemRole>(entity =>
        {
            entity.ToTable("system_roles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<UserRoleMapping>(entity =>
        {
            entity.ToTable("user_role_mappings");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne(x => x.User).WithMany(x => x.RoleMappings).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Role).WithMany(x => x.UserMappings).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- 2. СПРАВОЧНИКИ И СТРУКТУРА ---
        modelBuilder.Entity<AgentGroup>(entity =>
        {
            entity.ToTable("agent_groups");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ServiceCatalog>(entity =>
        {
            entity.ToTable("service_catalog");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FormTemplate>(entity =>
        {
            entity.ToTable("form_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FieldsSchema).HasColumnType("jsonb");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.ToTable("ticket_types");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<TicketStatus>(entity =>
        {
            entity.ToTable("ticket_statuses");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.IsSlaPausing).HasDefaultValue(false);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
        });

        modelBuilder.Entity<TicketStatusTransition>(entity =>
        {
            entity.ToTable("ticket_status_transitions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FromStatusId, x.ToStatusId }).IsUnique();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.FromStatus).WithMany().HasForeignKey(x => x.FromStatusId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToStatus).WithMany().HasForeignKey(x => x.ToStatusId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CriticalityLevel>(entity =>
        {
            entity.ToTable("criticality_levels");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<SlaPolicy>(entity =>
        {
            entity.ToTable("sla_policies");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<SlaValue>(entity =>
        {
            entity.ToTable("sla_values");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TicketTypeId, x.CriticalityLevelId }).IsUnique();
            entity.Property(x => x.IsDisabled).HasDefaultValue(false);
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.TicketType).WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CriticalityLevel).WithMany().HasForeignKey(x => x.CriticalityLevelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkingSchedule>(entity =>
        {
            entity.ToTable("working_schedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ScheduleType).HasDefaultValue("24x7");
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<WorkingHour>(entity =>
        {
            entity.ToTable("working_hours");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.WorkingScheduleId, x.DayOfWeek }).IsUnique();

            entity.HasOne(x => x.WorkingSchedule)
                  .WithMany(s => s.Hours)
                  .HasForeignKey(x => x.WorkingScheduleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssignmentSetting>(entity =>
        {
            entity.ToTable("assignment_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mode).HasDefaultValue("auto");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.ManualAssigner)
                  .WithMany()
                  .HasForeignKey(x => x.ManualAssignerUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CalendarException>(entity =>
        {
            entity.ToTable("calendar_exceptions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ExceptionDate).IsUnique();
        });

        // --- 3. АКТИВЫ (ITAM) ---
        modelBuilder.Entity<AssetClass>(entity =>
        {
            entity.ToTable("asset_classes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<AssetClassAttribute>(entity =>
        {
            entity.ToTable("asset_class_attributes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AssetClassId, x.Code }).IsUnique();
            entity.Property(x => x.DataType).HasDefaultValue("string");
            entity.Property(x => x.IsRequired).HasDefaultValue(false);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.AssetClass)
                  .WithMany(c => c.Attributes)
                  .HasForeignKey(x => x.AssetClassId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("assets");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InventoryNumber).IsUnique();
            entity.HasIndex(x => x.LifecycleStage);
            entity.Property(x => x.LifecycleStage).HasDefaultValue("Purchased");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.AssignedUser)
                  .WithMany(u => u.AssignedAssets)
                  .HasForeignKey(x => x.AssignedUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssetClass)
                  .WithMany(c => c.Assets)
                  .HasForeignKey(x => x.AssetClassId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssetMovement>(entity =>
        {
            entity.ToTable("asset_movements");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AssetId);
            entity.HasIndex(x => x.PerformedAt);
            entity.Property(x => x.PerformedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Asset).WithMany(a => a.Movements).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FromUser).WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToUser).WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PerformedBy).WithMany().HasForeignKey(x => x.PerformedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssetHistoryLog>(entity =>
        {
            entity.ToTable("asset_history_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChangedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Asset).WithMany(a => a.HistoryLogs).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany(u => u.AssetHistoryActions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OldAssignedUser).WithMany(u => u.PreviousAssetAssignments).HasForeignKey(x => x.OldAssignedUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.NewAssignedUser).WithMany(u => u.NewAssetAssignments).HasForeignKey(x => x.NewAssignedUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- 4. ЗАЯВКИ (TICKETS) ---
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CustomFieldsData).HasColumnType("jsonb");
            entity.Property(x => x.IsReactionEscalated).HasDefaultValue(false);
            entity.Property(x => x.IsResolutionEscalated).HasDefaultValue(false);
            entity.Property(x => x.IsSlaPaused).HasDefaultValue(false);
            entity.Property(x => x.SlaTimerPaused).HasDefaultValue(false);
            entity.Property(x => x.SlaAccumulatedPauseMinutes).HasDefaultValue(0);
            entity.Property(x => x.EscalationLevel).HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Creator).WithMany(x => x.CreatedTickets).HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Assignee).WithMany(x => x.AssignedTickets).HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CriticalityLevel).WithMany().HasForeignKey(x => x.CriticalityLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssigneeGroup).WithMany(g => g.Tickets).HasForeignKey(x => x.AssigneeGroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SlaPolicy).WithMany().HasForeignKey(x => x.SlaPolicyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- 5. МАППИНГИ И СВЯЗИ ---
        modelBuilder.Entity<TicketAssetMapping>(entity =>
        {
            entity.ToTable("ticket_asset_mappings");
            entity.HasKey(x => new { x.TicketId, x.AssetId });

            entity.HasOne(x => x.Ticket).WithMany(x => x.AssetMappings).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany(x => x.TicketMappings).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReleaseTicketMapping>(entity =>
        {
            entity.ToTable("release_ticket_mappings");
            entity.HasKey(x => new { x.ReleaseTicketId, x.TaskTicketId });

            entity.HasOne(x => x.ReleaseTicket).WithMany(x => x.ReleaseMappings).HasForeignKey(x => x.ReleaseTicketId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TaskTicket).WithMany(x => x.IncludedInReleases).HasForeignKey(x => x.TaskTicketId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- 6. СОГЛАСОВАНИЯ И КОММУНИКАЦИИ ---
        modelBuilder.Entity<TicketApproval>(entity =>
        {
            entity.ToTable("ticket_approvals");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TicketId, x.StepNumber });
            entity.Property(x => x.Status).HasDefaultValue("Pending");

            entity.HasOne(x => x.Ticket).WithMany(x => x.Approvals).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Approver).WithMany(u => u.Approvals).HasForeignKey(x => x.ApproverId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KnowledgeBaseArticle>(entity =>
        {
            entity.ToTable("knowledge_base_articles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IsPublished).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.ToTable("ticket_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IsInternal).HasDefaultValue(false);
            entity.Property(x => x.SourceType).HasDefaultValue("web");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Ticket).WithMany(x => x.Messages).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Author).WithMany(u => u.TicketMessages).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketAuditLog>(entity =>
        {
            entity.ToTable("ticket_audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChangedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Ticket).WithMany(x => x.AuditLogs).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany(u => u.TicketAuditLogs).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- 7. ТЕНАНТЫ, СПРАВОЧНИКИ, АУДИТ ---
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Dictionary>(entity =>
        {
            entity.ToTable("dictionaries");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.IsSystem).HasDefaultValue(false);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<DictionaryValue>(entity =>
        {
            entity.ToTable("dictionary_values");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DictionaryId, x.Code }).IsUnique();
            entity.Property(x => x.IsArchived).HasDefaultValue(false);
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.Dictionary).WithMany(d => d.Values).HasForeignKey(x => x.DictionaryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoginAudit>(entity =>
        {
            entity.ToTable("login_audits");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Username);
            entity.HasIndex(x => x.AttemptedAt);
            entity.Property(x => x.AttemptedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.Success).HasDefaultValue(false);

            entity.HasOne(x => x.User).WithMany(u => u.LoginAudits).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PasswordPolicy>(entity =>
        {
            entity.ToTable("password_policies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MinLength).HasDefaultValue(8);
            entity.Property(x => x.RequireDigit).HasDefaultValue(true);
            entity.Property(x => x.RequireUppercase).HasDefaultValue(false);
            entity.Property(x => x.RequireLowercase).HasDefaultValue(true);
            entity.Property(x => x.RequireSpecial).HasDefaultValue(false);
            entity.Property(x => x.MaxFailedAttempts).HasDefaultValue(5);
            entity.Property(x => x.LockoutMinutes).HasDefaultValue(15);
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(x => x.User).WithMany(u => u.AuditLogs).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ПОСТ-ОБРАБОТКА
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
        {
            if (foreignKey.DeleteBehavior == DeleteBehavior.Cascade)
                continue;

            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}