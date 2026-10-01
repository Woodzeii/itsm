using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface IDictionaryService
{
    Task<List<DictionaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<DictionaryDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<DictionaryDto> CreateAsync(CreateDictionaryRequest request, CancellationToken ct = default);
    Task<DictionaryDto?> UpdateAsync(int id, UpdateDictionaryRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<List<DictionaryValueDto>> GetValuesAsync(int dictionaryId, bool includeArchived = false, CancellationToken ct = default);
    Task<DictionaryValueDto?> CreateValueAsync(int dictionaryId, CreateDictionaryValueRequest request, CancellationToken ct = default);
    Task<DictionaryValueDto?> UpdateValueAsync(int dictionaryId, int valueId, UpdateDictionaryValueRequest request, CancellationToken ct = default);
    Task<bool> DeleteValueAsync(int dictionaryId, int valueId, CancellationToken ct = default);
    Task<DictionaryValueDto?> ArchiveValueAsync(int dictionaryId, int valueId, CancellationToken ct = default);
}