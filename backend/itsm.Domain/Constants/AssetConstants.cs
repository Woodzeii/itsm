namespace itsm.Domain.Constants;

/// <summary>
/// Этапы жизненного цикла актива (АКТ-12).
/// </summary>
public static class AssetLifecycleStages
{
    public const string Purchased = "Purchased";           // Закуплен
    public const string InStock = "InStock";               // На складе
    public const string InUse = "InUse";                   // В эксплуатации
    public const string InRepair = "InRepair";             // В ремонте
    public const string Decommissioned = "Decommissioned"; // Списан

    public static readonly IReadOnlyList<string> All = new[]
    {
        Purchased, InStock, InUse, InRepair, Decommissioned
    };

    public static string GetRussianName(string stage) => stage switch
    {
        Purchased => "Закуплен",
        InStock => "На складе",
        InUse => "В эксплуатации",
        InRepair => "В ремонте",
        Decommissioned => "Списан",
        _ => stage
    };
}

/// <summary>
/// Виды движения актива (АКТ-10).
/// </summary>
public static class AssetMovementTypes
{
    public const string Transfer = "transfer";     // Передача другому сотруднику
    public const string Relocate = "relocate";     // Перемещение в другое место/подразделение
    public const string Issue = "issue";           // Выдача со склада
    public const string Return = "return";         // Возврат на склад

    public static readonly IReadOnlyList<string> All = new[]
    {
        Transfer, Relocate, Issue, Return
    };

    public static string GetRussianName(string type) => type switch
    {
        Transfer => "Передача другому сотруднику",
        Relocate => "Перемещение",
        Issue => "Выдача со склада",
        Return => "Возврат на склад",
        _ => type
    };
}