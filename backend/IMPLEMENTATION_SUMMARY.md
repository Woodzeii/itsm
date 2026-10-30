# Внедрение HasQueryFilter для TenantId (НФТ-02) - Сводка изменений

## 📋 Обзор

Реализовано автоматическое разграничение данных арендаторов на уровне СУБД с использованием Entity Framework Core `HasQueryFilter`. Все запросы к мультитенантным сущностям автоматически фильтруются по `TenantId`.

## 🎯 Решённые проблемы

✅ **Безопасность данных** - предотвращение утечки данных между арендаторами на уровне БД  
✅ **Автоматизация** - исключение необходимости добавления `.Where(e => e.TenantId == current)` к каждому запросу  
✅ **Масштабируемость** - простота добавления новых мультитенантных сущностей  
✅ **Перформанс** - фильтрация на уровне БД сокращает передачу данных  

## 📁 Новые файлы

### 1. **IMultiTenant интерфейс**
- **Файл**: `itsm.Domain/Entities/IMultiTenant.cs`
- **Описание**: Интерфейс для всех мультитенантных сущностей
```csharp
public interface IMultiTenant
{
    int TenantId { get; set; }
}
```

### 2. **TenantContext сервис**
- **Файл**: `itsm.Infrastructure/Persistence/TenantContext.cs`
- **Описание**: Управление текущим TenantId через AsyncLocal
```csharp
public interface ITenantContext
{
    int GetCurrentTenantId();
    void SetCurrentTenantId(int tenantId);
}
```

### 3. **TenantIdMiddleware**
- **Файл**: `itsm.Infrastructure/Security/TenantIdMiddleware.cs`
- **Описание**: Middleware для автоматической установки TenantId из JWT или заголовка
- **Приоритет**:
  1. JWT claim "tenant_id"
  2. HTTP заголовок "X-Tenant-Id"
  3. Значение по умолчанию (1)

### 4. Документация
- **MULTITENANCY_IMPLEMENTATION.md** - Полное руководство по внедрению
- **MIGRATIONS_GUIDE.md** - Примеры SQL миграций

## 🔄 Изменённые файлы

### 1. **Сущности (Domain)**

Все следующие сущности теперь реализуют `IMultiTenant`:

| Сущность | Файл | Изменение |
|----------|------|-----------|
| User | User.cs | `TenantId` (изменена с `int?` на `int` - обязательное) |
| Ticket | Ticket.cs | Добавлен `TenantId` |
| ServiceCatalog | ServiceCatalog.cs | Добавлен `TenantId` |
| Asset | Asset.cs | Добавлен `TenantId` |
| AgentGroup | AgentGroup.cs | Добавлен `TenantId` |
| TicketType | TicketType.cs | Добавлен `TenantId` |
| TicketStatus | TicketStatus.cs | Добавлен `TenantId` |
| CriticalityLevel | CriticalityLevel.cs | Добавлен `TenantId` |
| FormTemplate | FormTemplate.cs | Добавлен `TenantId` |

### 2. **ItsmDbContext**

**Файл**: `itsm.Infrastructure/Persistence/ItsmDbContext.cs`

**Главные изменения**:

```csharp
public class ItsmDbContext(
    DbContextOptions<ItsmDbContext> options, 
    ITenantContext? tenantContext = null) // Dependency Injection
    : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext ?? new TenantContext();

    // Применение HasQueryFilter для всех мультитенантных сущностей
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... конфигурация сущностей ...
        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        // Автоматически применяет фильтр ко всем IMultiTenant сущностям
        // Фильтр: e => e.TenantId == _tenantContext.GetCurrentTenantId()
    }

    public void SetTenantId(int tenantId) { }
    public int GetCurrentTenantId() { }
}
```

**Изменения конфигурации сущностей**:

- Добавлены индексы на `TenantId` для оптимизации
- Обновлены составные индексы на `(TenantId, Code)` для уникальности в пределах тенанта
- Обновлены seed данные с явным указанием `TenantId = 1`

**Примеры индексов**:
```csharp
// Простой индекс
entity.HasIndex(x => x.TenantId);

// Составной индекс для уникальности
entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
```

### 3. **DependencyInjection.cs**

**Файл**: `itsm.Infrastructure/DependencyInjection.cs`

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Регистрируем TenantContext для DI
    services.AddScoped<ITenantContext, TenantContext>();

    // DbContext получит ITenantContext через конструктор
    services.AddDbContext<ItsmDbContext>((provider, options) =>
    {
        var tenantContext = provider.GetRequiredService<ITenantContext>();
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
    });
}
```

### 4. **Program.cs**

**Файл**: `itsm.WebApi/Program.cs`

```csharp
// Middleware для установки TenantId должно быть добавлено
// после UseAuthentication() и перед UseAuthorization()
app.UseAuthentication();
app.UseTenantIdMiddleware();  // ← Новая строка
app.UseAuthorization();
```

## 🗄️ Требуемые изменения БД

### Миграция EF Core

```bash
cd backend
dotnet ef migrations add AddTenantIdToMultiTenantEntities
dotnet ef database update
```

### SQL структура

Все таблицы мультитенантных сущностей получат новую колонку:

```sql
ALTER TABLE users ADD COLUMN tenant_id INTEGER NOT NULL DEFAULT 1;
ALTER TABLE tickets ADD COLUMN tenant_id INTEGER NOT NULL DEFAULT 1;
-- ... остальные таблицы
```

И индексы:

```sql
CREATE INDEX idx_users_tenant_id ON users(tenant_id);
CREATE UNIQUE INDEX idx_users_tenant_username ON users(tenant_id, username);
-- ... остальные индексы
```

## 🚀 Использование

### Вариант 1: Через middleware (автоматически)

```csharp
// TenantId автоматически извлекается из JWT или заголовка
// Middleware вызывает: dbContext.SetTenantId(tenantId)
GET /api/tickets?page=1  // Header: X-Tenant-Id: 1
```

### Вариант 2: Вручную в контроллере

```csharp
[HttpGet]
public async Task<IActionResult> GetTickets()
{
    var tenantId = int.Parse(User.FindFirst("tenant_id")?.Value ?? "1");
    _dbContext.SetTenantId(tenantId);
    
    var tickets = await _dbContext.Tickets.ToListAsync(); // Автофильтр!
    return Ok(tickets);
}
```

### Вариант 3: В сервис/бизнес-логике

```csharp
public class TicketService
{
    public async Task<List<Ticket>> GetActiveTicketsAsync(int tenantId)
    {
        _dbContext.SetTenantId(tenantId);
        return await _dbContext.Tickets
            .Where(t => t.Status.Code != "closed")
            .ToListAsync();
    }
}
```

## ✨ Дополнительные преимущества

- **Для разработчиков**: Невозможно случайно забыть фильтр по TenantId
- **Для админов**: Встроенная защита от утечки данных
- **Для тестов**: Легко мокировать `ITenantContext`
- **Для масштабирования**: Просто добавить новую сущность, реализующую `IMultiTenant`

## ⚠️ Важные замечания

1. **Обязательно устанавливайте TenantId** перед запросами, иначе получите пустой результат
2. **User.TenantId теперь обязательное поле** (было `int?`, стало `int`)
3. **Уникальные индексы изменились** - теперь включают TenantId
4. **Для отладки** используйте `dbContext.Database.GeneratedCode()` для просмотра сгенерированного SQL

## 📊 Структура решения

```
Infrastructure/
├── Persistence/
│   ├── ItsmDbContext.cs         (~ HasQueryFilter logic)
│   ├── TenantContext.cs         (~ ITenantContext)
│   └── Migrations/              (~ SQL migrations)
├── Security/
│   └── TenantIdMiddleware.cs    (~ Middleware)
└── DependencyInjection.cs       (~ DI registration)

Domain/
└── Entities/
    ├── IMultiTenant.cs          (~ Interface)
    ├── User.cs                  (✓ IMultiTenant)
    ├── Ticket.cs                (✓ IMultiTenant)
    ├── Asset.cs                 (✓ IMultiTenant)
    └── ... (остальные)

WebApi/
└── Program.cs                   (+ UseTenantIdMiddleware)
```

## 🎓 Дальнейшие улучшения

Возможные расширения:

1. **AuditLog с TenantId** - отслеживание операций в контексте тенанта
2. **LoginAudit с TenantId** - логирование входов в контексте
3. **Soft Delete** - мягкое удаление с учётом TenantId
4. **Tenant-specific settings** - настройки, специфичные для каждого тенанта
5. **Rate Limiting по TenantId** - ограничение по арендатору

---

**Статус**: ✅ Реализовано и документировано  
**Версия**: 1.0.0  
**Тестирование**: Рекомендуется тестировать с использованием различных TenantId
