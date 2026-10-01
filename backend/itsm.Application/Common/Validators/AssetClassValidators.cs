using FluentValidation;
using itsm.Application.Common.Models;

namespace itsm.Application.Common.Validators;

public static class AssetClassAttributeDataTypes
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "string", "text", "number", "date", "bool", "dictionary", "asset"
    };

    public static bool IsValid(string dataType) => All.Contains(dataType);
}

// ==================== AssetClass ====================

public class CreateAssetClassRequestValidator : AbstractValidator<CreateAssetClassRequest>
{
    public CreateAssetClassRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Код класса обязателен")
            .MinimumLength(2).MaximumLength(64)
            .Matches("^[a-z0-9_]+$")
            .WithMessage("Код — только строчные латинские буквы, цифры и подчёркивание");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Наименование класса обязательно")
            .MaximumLength(255);
    }
}

public class UpdateAssetClassRequestValidator : AbstractValidator<UpdateAssetClassRequest>
{
    public UpdateAssetClassRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Наименование класса обязательно")
            .MaximumLength(255);
    }
}

// ==================== AssetClassAttribute ====================

public class CreateAssetClassAttributeRequestValidator : AbstractValidator<CreateAssetClassAttributeRequest>
{
    public CreateAssetClassAttributeRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Код атрибута обязателен")
            .MinimumLength(1).MaximumLength(64)
            .Matches("^[a-z][a-z0-9_]*$")
            .WithMessage("Код — только строчные латинские буквы, цифры и подчёркивание, начинается с буквы");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Наименование атрибута обязательно")
            .MaximumLength(255);

        RuleFor(x => x.DataType)
            .NotEmpty().WithMessage("Тип данных обязателен")
            .Must(AssetClassAttributeDataTypes.IsValid)
            .WithMessage($"Недопустимый тип данных. Допустимые: {string.Join(", ", AssetClassAttributeDataTypes.All)}");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Порядок сортировки не может быть отрицательным");

        RuleFor(x => x.DefaultValue)
            .MaximumLength(1000)
            .When(x => x.DefaultValue is not null);

        RuleFor(x => x.Options)
            .MaximumLength(4000)
            .When(x => x.Options is not null);
    }
}

public class UpdateAssetClassAttributeRequestValidator : AbstractValidator<UpdateAssetClassAttributeRequest>
{
    public UpdateAssetClassAttributeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Наименование атрибута обязательно")
            .MaximumLength(255);

        RuleFor(x => x.DataType)
            .NotEmpty().WithMessage("Тип данных обязателен")
            .Must(AssetClassAttributeDataTypes.IsValid)
            .WithMessage($"Недопустимый тип данных. Допустимые: {string.Join(", ", AssetClassAttributeDataTypes.All)}");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Порядок сортировки не может быть отрицательным");

        RuleFor(x => x.DefaultValue)
            .MaximumLength(1000)
            .When(x => x.DefaultValue is not null);

        RuleFor(x => x.Options)
            .MaximumLength(4000)
            .When(x => x.Options is not null);
    }
}