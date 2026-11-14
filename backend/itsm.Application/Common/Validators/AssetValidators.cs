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

        RuleFor(request => request.AssetClassCode)
            .NotEmpty().WithMessage("Выберите класс актива.")
            .MaximumLength(64);

        RuleFor(request => request.WarehouseCode).MaximumLength(64);
        RuleFor(request => request.LocationCode).MaximumLength(64);
        RuleFor(request => request.DepartmentCode).MaximumLength(64);

        RuleFor(request => request.InventoryNumber)
            .MaximumLength(64);

        RuleFor(request => request.SerialNumber)
            .MaximumLength(64);

        RuleFor(request => request.Category)
            .MaximumLength(100);

        RuleFor(request => request.LifecycleStage)
            .Must(stage => stage is null)
            .WithMessage("Этап жизненного цикла назначается сервером.");

        RuleFor(request => request.AssignedUserId)
            .Must(userId => userId is null)
            .WithMessage("Сотрудника можно назначить только при выдаче актива со склада.");
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

        RuleFor(request => request.AssetClassCode).MaximumLength(64);
        RuleFor(request => request.WarehouseCode).MaximumLength(64);
        RuleFor(request => request.LocationCode).MaximumLength(64);
        RuleFor(request => request.DepartmentCode).MaximumLength(64);

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

        RuleFor(request => request.ToWarehouseCode).MaximumLength(64);
        RuleFor(request => request.ToLocationCode).MaximumLength(64);
        RuleFor(request => request.ToDepartmentCode).MaximumLength(64);
    }
}
