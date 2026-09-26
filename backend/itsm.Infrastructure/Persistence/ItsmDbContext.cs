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
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<CalendarException> CalendarExceptions => Set<CalendarException>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetHistoryLog> AssetHistoryLogs => Set<AssetHistoryLog>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketAssetMapping> TicketAssetMappings => Set<TicketAssetMapping>();
    public DbSet<ReleaseTicketMapping> ReleaseTicketMappings => Set<ReleaseTicketMapping>();
    public DbSet<TicketApproval> TicketApprovals => Set<TicketApproval>();
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles => Set<KnowledgeBaseArticle>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAuditLog> TicketAuditLogs => Set<TicketAuditLog>();

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
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        
        // Жесткая иерархия Руководитель -> Подчиненные (для шага 1 согласования доступов)
        entity.HasOne(x => x.Manager)
              .WithMany(x => x.DirectReports)
              .HasForeignKey(x => x.ManagerId)
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
    });

    modelBuilder.Entity<SlaPolicy>(entity =>
    {
        entity.ToTable("sla_policies");
        entity.HasKey(x => x.Id);
    });

    modelBuilder.Entity<CalendarException>(entity =>
    {
        entity.ToTable("calendar_exceptions");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.ExceptionDate).IsUnique();
    });

    // --- 3. АКТИВЫ (ITAM) ---
    modelBuilder.Entity<Asset>(entity =>
    {
        entity.ToTable("assets");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.InventoryNumber).IsUnique();
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        
        entity.HasOne(x => x.AssignedUser)
              .WithMany(u => u.AssignedAssets)
              .HasForeignKey(x => x.AssignedUserId)
              .OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<AssetHistoryLog>(entity =>
    {
        entity.ToTable("asset_history_logs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ChangedAt).HasDefaultValueSql("now()");
        
        entity.HasOne(x => x.User).WithMany(x => x.AssetHistoryActions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.OldAssignedUser).WithMany(x => x.PreviousAssetAssignments).HasForeignKey(x => x.OldAssignedUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.NewAssignedUser).WithMany(x => x.NewAssetAssignments).HasForeignKey(x => x.NewAssignedUserId).OnDelete(DeleteBehavior.Restrict);
    });

    // --- 4. ЗАЯВКИ (TICKETS) ---
    modelBuilder.Entity<Ticket>(entity =>
    {
        entity.ToTable("tickets");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.CustomFieldsData).HasColumnType("jsonb");
        entity.Property(x => x.IsReactionEscalated).HasDefaultValue(false);
        entity.Property(x => x.IsResolutionEscalated).HasDefaultValue(false);
        entity.Property(x => x.SlaTimerPaused).HasDefaultValue(false);
        entity.Property(x => x.SlaAccumulatedPauseMinutes).HasDefaultValue(0);
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        
        // Связи участников процесса
        entity.HasOne(x => x.Creator).WithMany(x => x.CreatedTickets).HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Assignee).WithMany(x => x.AssignedTickets).HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        
        // Остальные базовые связи заявки
        entity.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.AssigneeGroup).WithMany(g => g.Tickets).HasForeignKey(x => x.AssigneeGroupId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.SlaPolicy).WithMany().HasForeignKey(x => x.SlaPolicyId).OnDelete(DeleteBehavior.Restrict);
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

    // ПОСТ-ОБРАБОТКА: Безопасный Restrict по умолчанию для всех ОСТАЛЬНЫХ внешних ключей, 
    // которые не были переопределены выше (например, связи логов или статей БЗ)
    foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
    {
        if (foreignKey.DeleteBehavior == DeleteBehavior.Cascade)
            continue; // Сохраняем Cascade для промежуточных таблиц (маппингов)
            
        foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
    }
    }
}
