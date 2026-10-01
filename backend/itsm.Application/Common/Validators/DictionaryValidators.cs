using FluentValidation;
using itsm.Application.Common.Models;

namespace itsm.Application.Common.Validators;

public class CreateDictionaryRequestValidator : AbstractValidator<CreateDictionaryRequest>
{
    public CreateDictionaryRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Код обязателен")
            .MinimumLength(2).MaximumLength(64)
            .Matches("^[a-z0-9_]+$")
            .WithMessage("Код — только строчные латинские буквы, цифры и подчёркивание");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название обязательно")
            .MaximumLength(255);
    }
}

public class UpdateDictionaryRequestValidator : AbstractValidator<UpdateDictionaryRequest>
{
    public UpdateDictionaryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название обязательно")
            .MaximumLength(255);
    }
}

public class CreateDictionaryValueRequestValidator : AbstractValidator<CreateDictionaryValueRequest>
{
    public CreateDictionaryValueRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Код значения обязателен")
            .MinimumLength(1).MaximumLength(64)
            .Matches("^[a-zA-Z0-9_.-]+$")
            .WithMessage("Код — только латиница, цифры, точка, дефис, подчёркивание");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название значения обязательно")
            .MaximumLength(255);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Порядок сортировки не может быть отрицательным");
    }
}

public class UpdateDictionaryValueRequestValidator : AbstractValidator<UpdateDictionaryValueRequest>
{
    public UpdateDictionaryValueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название обязательно")
            .MaximumLength(255);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Порядок сортировки не может быть отрицательным");
    }
}