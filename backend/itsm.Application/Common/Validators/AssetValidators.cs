using FluentValidation;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;

namespace itsm.Application.Common.Validators;

public sealed class CreateAssetRequestValidator : AbstractValidator<CreateAssetRequest>
{
    public CreateAssetRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Укажите наименование актива.")
            .MaximumLength(200);

        RuleFor(request => request.AssetClassId)
            .GreaterThan(0).WithMessage("Выберите класс актива.");

        RuleFor(request => request.InventoryNumber)
            .MaximumLength(64);

        RuleFor(request => request.SerialNumber)
            .MaximumLength(64);

        RuleFor(request => request.Category)
            .MaximumLength(100);

        RuleFor(request => request.LifecycleStage)
            .Must(stage => stage is null || AssetLifecycleStages.All.Contains(stage))
            .WithMessage("Неизвестный этап жизненного цикла актива.");
    }
}

public sealed class UpdateAssetRequestValidator : AbstractValidator<UpdateAssetRequest>
{
    public UpdateAssetRequestValidator()
    {
        RuleFor(request => request.Name)
            .MaximumLength(200)
            .Must(name => name is null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("Наименование актива не может быть пустым.");

        RuleFor(request => request.AssetClassId)
            .GreaterThan(0)
            .When(request => request.AssetClassId is not null);

        RuleFor(request => request.InventoryNumber)
            .MaximumLength(64);

        RuleFor(request => request.SerialNumber)
            .MaximumLength(64);

        RuleFor(request => request.Category)
            .MaximumLength(100);
    }
}

public sealed class RegisterAssetMovementRequestValidator : AbstractValidator<RegisterAssetMovementRequest>
{
    public RegisterAssetMovementRequestValidator()
    {
        RuleFor(request => request.MovementType)
            .Must(type => AssetMovementTypes.All.Contains(type))
            .WithMessage("Неизвестный тип движения актива.");

        RuleFor(request => request.Comment)
            .MaximumLength(2000);
    }
}
