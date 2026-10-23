using FluentValidation;
using itsm.Application.Common.Models;

namespace itsm.Application.Common.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Логин обязателен")
            .MinimumLength(3).WithMessage("Логин должен быть не короче 3 символов")
            .MaximumLength(64)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Логин может содержать только буквы, цифры, точку, дефис и подчёркивание");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен")
            .EmailAddress().WithMessage("Некорректный формат email")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен")
            .MinimumLength(8).WithMessage("Пароль должен содержать минимум 8 символов")
            .Matches("[A-Za-z]").WithMessage("Пароль должен содержать хотя бы одну букву")
            .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру");

        RuleFor(x => x.FullName).MaximumLength(255);
        RuleFor(x => x.Department).MaximumLength(255);
        RuleFor(x => x.RoleIds).NotNull();
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Логин обязателен")
            .MinimumLength(3).WithMessage("Логин должен быть не короче 3 символов")
            .MaximumLength(64)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Логин может содержать только буквы, цифры, точку, дефис и подчёркивание");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен")
            .EmailAddress().WithMessage("Некорректный формат email")
            .MaximumLength(255);

        RuleFor(x => x.FullName).MaximumLength(255);
        RuleFor(x => x.Department).MaximumLength(255);
        RuleFor(x => x.RoleIds).NotEmpty().When(x => x.RoleIds is not null)
            .WithMessage("Пользователю должна быть назначена хотя бы одна роль");
    }
}

public class SetUserRolesRequestValidator : AbstractValidator<SetUserRolesRequest>
{
    public SetUserRolesRequestValidator()
    {
        RuleFor(x => x.RoleIds).NotNull().WithMessage("Список ролей обязателен");
    }
}

public class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Search).MaximumLength(255);
    }
}
