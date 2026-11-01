using FluentValidation;
using itsm.Application.Common.Models;

namespace itsm.Application.Common.Validators;

public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Название тенанта обязательно")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(255);

        RuleFor(request => request.FirstAdminLogin)
            .NotEmpty().WithMessage("Логин администратора обязателен")
            .MinimumLength(3)
            .MaximumLength(64)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Логин может содержать только буквы, цифры, точку, дефис и подчёркивание");

        RuleFor(request => request.FirstAdminEmail)
            .NotEmpty().WithMessage("Email администратора обязателен")
            .EmailAddress().WithMessage("Некорректный формат email")
            .MaximumLength(255);

        RuleFor(request => request.FirstAdminFullName)
            .NotEmpty().WithMessage("ФИО администратора обязательно")
            .Must(fullName => !string.IsNullOrWhiteSpace(fullName))
            .MaximumLength(255);

        RuleFor(request => request.FirstAdminPassword)
            .NotEmpty().WithMessage("Пароль администратора обязателен")
            .MinimumLength(8).WithMessage("Пароль должен содержать минимум 8 символов")
            .Matches("[A-Za-z]").WithMessage("Пароль должен содержать хотя бы одну букву")
            .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру");

        RuleFor(request => request.MaxUsers).GreaterThan(0).When(request => request.MaxUsers.HasValue);
        RuleFor(request => request.MaxAssets).GreaterThan(0).When(request => request.MaxAssets.HasValue);
        RuleFor(request => request.MaxTickets).GreaterThan(0).When(request => request.MaxTickets.HasValue);
        RuleFor(request => request.MaxStorageBytes).GreaterThan(0).When(request => request.MaxStorageBytes.HasValue);
    }
}
