# Реализация HasQueryFilter для автоматического разграничения данных арендаторов (НФТ-02)

## Обзор

Данная реализация внедряет паттерн **HasQueryFilter** в Entity Framework Core для автоматического фильтрования данных на уровне базы данных по `TenantId`. Это обеспечивает безопасное разграничение данных в мультитенантном приложении, предотвращая утечку данных между арендаторами.

## Архитектура

### 1. Интерфейс IMultiTenant

Все мультитенантные сущности реализуют интерфейс `IMultiTenant`:

```csharp
public interface IMultiTenant
{
    int TenantId { get; set; }
}
```

### 2. Сущности с поддержкой мультитенантности

Следующие сущности реализуют `IMultiTenant`:

- **User** - пользователи
- **Ticket** - заявки
- **ServiceCatalog** - каталог услуг
- **Asset** - активы (имущество)
- **AgentGroup** - группы инженеров
- **TicketType** - типы заявок
- **TicketStatus** - статусы заявок
- **CriticalityLevel** - уровни критичности
- **FormTemplate** - шаблоны форм

### 3. TenantContext

Реализует интерфейс `ITenantContext` для хранения текущего `TenantId`:

```csharp
public interface ITenantContext
{
    int GetCurrentTenantId();
    void SetCurrentTenantId(int tenantId);
}
```

Использует `AsyncLocal<int>` для безопасного хранения значения в асинхронных операциях.

### 4. ItsmDbContext с HasQueryFilter

В методе `OnModelCreating` применяется фильтр ко всем мультитенантным сущностям:

```csharp
private void ApplyTenantFilters(ModelBuilder modelBuilder)
{
    var multiTenantTypes = modelBuilder.Model
        .GetEntityTypes()
        .Where(x => typeof(IMultiTenant).IsAssignableFrom(x.ClrType))
        .ToList();

    foreach (var entityType in multiTenantTypes)
    {
        // Фильтр: e => e.TenantId == _tenantContext.GetCurrentTenantId()
        // Автоматически применяется ко всем запросам
        entityType.SetQueryFilter(lambda);
    }
}
```

## Использование

### Регистрация в DependencyInjection

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Регистрируем TenantContext
    services.AddScoped<ITenantContext, TenantContext>();
    
    // DbContext получит ITenantContext через конструктор
    services.AddDbContext<ItsmDbContext>((provider, options) =>
    {
        var tenantContext = provider.GetRequiredService<ITenantContext>();
        options.UseNpgsql(configuration.GetConnectionString("ItsmDatabase"));
    });
    
    return services;
}
```

### Установка TenantId в middleware или контроллере

```csharp
// В middleware
app.Use(async (context, next) =>
{
    var tenantId = GetTenantIdFromRequest(context);
    var dbContext = context.RequestServices.GetRequiredService<ItsmDbContext>();
    dbContext.SetTenantId(tenantId);
    await next();
});

// Или в контроллере
[ApiController]
public class TicketsController : ControllerBase
{
    private readonly ItsmDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public TicketsController(ItsmDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetTickets()
    {
        var tenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
        _tenantContext.SetCurrentTenantId(tenantId);
        
        // Все запросы автоматически будут отфильтрованы по TenantId
        var tickets = await _dbContext.Tickets.ToListAsync();
        return Ok(tickets);
    }
}
```

## Преимущества

✅ **Безопасность на уровне БД** - фильтр применяется автоматически, невозможно забыть `.Where(t => t.TenantId == ...)`

✅ **Производительность** - фильтр применяется на уровне БД, сокращает размер результатов

✅ **Масштабируемость** - нет необходимости вручную добавлять фильтр к каждому запросу

✅ **Типизация** - все мультитенантные сущности имеют единый интерфейс

✅ **Тестируемость** - `ITenantContext` легко мокируется в тестах

## Индексы базы данных

Для оптимизации производительности добавлены индексы на `TenantId`:

```sql
-- Индекс на TenantId для быстрого фильтрования
CREATE INDEX idx_users_tenant_id ON users(tenant_id);
CREATE INDEX idx_tickets_tenant_id ON tickets(tenant_id);
CREATE INDEX idx_assets_tenant_id ON assets(tenant_id);

-- Составные индексы для уникальности в рамках тенанта
CREATE UNIQUE INDEX idx_users_tenant_username ON users(tenant_id, username);
CREATE UNIQUE INDEX idx_ticket_types_tenant_code ON ticket_types(tenant_id, code);
CREATE UNIQUE INDEX idx_ticket_statuses_tenant_code ON ticket_statuses(tenant_id, code);
```

## Миграция существующей БД

При переходе с однотенантной архитектуры на мультитенантную:

1. Добавляется колонка `TenantId NOT NULL DEFAULT 1` для всех мультитенантных таблиц
2. Все существующие записи получают `TenantId = 1` (default tenant)
3. Добавляются новые индексы для оптимизации

## Пример миграции (EF Core)

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Добавляем колонку TenantId со значением по умолчанию
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

    // Добавляем индексы
    migrationBuilder.CreateIndex(
        name: "ix_users_tenant_id",
        table: "users",
        column: "tenant_id");

    migrationBuilder.CreateIndex(
        name: "ix_tickets_tenant_id",
        table: "tickets",
        column: "tenant_id");

    // Создаём составные индексы для уникальности
    migrationBuilder.CreateIndex(
        name: "ix_users_tenant_username",
        table: "users",
        columns: new[] { "tenant_id", "username" },
        unique: true);
}
```

## Тестирование

```csharp
[TestClass]
public class MultiTenantFilterTests
{
    private ItsmDbContext _context;
    private ITenantContext _tenantContext;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;

        _tenantContext = new TenantContext();
        _context = new ItsmDbContext(options, _tenantContext);
    }

    [TestMethod]
    public async Task GetTickets_FiltersBy TenantId()
    {
        // Arrange
        _tenantContext.SetCurrentTenantId(1);
        var user1 = new User { Id = 1, TenantId = 1, Username = "user1" };
        var user2 = new User { Id = 2, TenantId = 2, Username = "user2" };
        
        _context.Users.Add(user1);
        _context.Users.Add(user2);
        await _context.SaveChangesAsync();

        // Act
        var users = await _context.Users.ToListAsync();

        // Assert
        Assert.AreEqual(1, users.Count);
        Assert.AreEqual("user1", users[0].Username);
    }
}
```

## Важные замечания

⚠️ **Обязательно устанавливайте TenantId** перед выполнением запросов, иначе получите пустой результат

⚠️ **При создании новых записей** убедитесь, что `TenantId` установлен правильно

⚠️ **В миграциях** используйте `HasData()` с явным указанием `TenantId`

⚠️ **При отладке** помните, что фильтр работает на уровне БД и не видна в сгенерированном SQL исключением `context.Database.GeneratedCode()`

## Ссылки

- [Entity Framework Core - Query Filters](https://docs.microsoft.com/en-us/ef/core/querying/filters)
- [Multi-Tenancy with EF Core](https://www.baeldung.com/cs/multi-tenant-entity-framework-core)
- НФТ-02 - Требование разграничения данных на уровне БД

---

**Версия:** 1.0.0  
**Дата:** 2026-01-01  
**Автор:** Copilot
