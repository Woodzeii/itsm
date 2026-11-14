using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface IDictionaryService
{
    Task<List<DictionaryDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<DictionaryDetailDto?> GetByCodeAsync(string code, bool includeArchived = false, CancellationToken ct = default);

    Task<DictionaryDto> CreateAsync(CreateDictionaryRequest request, CancellationToken ct = default);
    Task<DictionaryDto?> UpdateAsync(string code, UpdateDictionaryRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string code, CancellationToken ct = default);
    Task<DictionaryDto?> ArchiveAsync(string code, CancellationToken ct = default);
    Task<DictionaryDto?> RestoreAsync(string code, CancellationToken ct = default);

    Task<List<DictionaryValueDto>?> GetValuesAsync(string dictionaryCode, bool includeArchived = false, CancellationToken ct = default);
    Task<DictionaryValueDto?> CreateValueAsync(string dictionaryCode, CreateDictionaryValueRequest request, CancellationToken ct = default);
    Task<DictionaryValueDto?> UpdateValueAsync(string dictionaryCode, string valueCode, UpdateDictionaryValueRequest request, CancellationToken ct = default);
    Task<bool> DeleteValueAsync(string dictionaryCode, string valueCode, CancellationToken ct = default);
    Task<DictionaryValueDto?> ArchiveValueAsync(string dictionaryCode, string valueCode, CancellationToken ct = default);
    Task<DictionaryValueDto?> RestoreValueAsync(string dictionaryCode, string valueCode, CancellationToken ct = default);
}