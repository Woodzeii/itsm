using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public enum UserAdminErrorKind
{
    NotFound,
    Conflict,
    Validation
}

public class UserAdminException : Exception
{
    public UserAdminErrorKind Kind { get; }

    public UserAdminException(UserAdminErrorKind kind, string message) : base(message)
    {
        Kind = kind;
    }
}

public interface IUserAdminService
{
    Task<PagedResult<UserAdminDto>> GetListAsync(UserListQuery query, CancellationToken ct = default);
    Task<List<RoleSummaryDto>> GetRolesAsync(CancellationToken ct = default);
    Task<UserAdminDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<UserAdminDto> CreateAsync(CreateUserRequest request, int actorId, string? ip, CancellationToken ct = default);
    Task<UserAdminDto> UpdateAsync(int id, UpdateUserRequest request, int actorId, string? ip, CancellationToken ct = default);

    /// <summary>Мягкое удаление: деактивация учётной записи.</summary>
    Task DeleteAsync(int id, int actorId, string? ip, CancellationToken ct = default);
    Task<UserAdminDto> SetActiveAsync(int id, bool isActive, int actorId, string? ip, CancellationToken ct = default);
    Task<UserAdminDto> SetRolesAsync(int id, SetUserRolesRequest request, int actorId, string? ip, CancellationToken ct = default);
    Task<UserAdminDto> ResetMfaAsync(int id, int actorId, string? ip, CancellationToken ct = default);
    Task<UserAdminDto> UnlockAsync(int id, int actorId, string? ip, CancellationToken ct = default);
}
