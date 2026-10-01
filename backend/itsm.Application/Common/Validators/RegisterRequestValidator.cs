using FluentValidation;
using itsm.Application.Common.Models;

namespace itsm.Application.Common.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
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
			.Must(email => !email.Contains(' ')).WithMessage("Email не должен содержать пробелов")
			.MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен")
            .MinimumLength(8).WithMessage("Пароль должен содержать минимум 8 символов")
            .Matches("[A-Za-z]").WithMessage("Пароль должен содержать хотя бы одну букву")
            .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру");

        RuleFor(x => x.FullName)
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.FullName));

        RuleFor(x => x.Department)
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.Department));
    }
}