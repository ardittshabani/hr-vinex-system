using FluentValidation;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Contracts.Users;

namespace Vinex.HR.Contracts.Validators;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Matches("^[a-zA-Z0-9._-]{3,50}$").WithMessage("Username must be 3-50 characters: letters, digits, '.', '_' or '-'.");
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("Display name is required.").MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Role).Must(r => RoleNames.All.Contains(r)).WithMessage("Select a valid role.");
        RuleFor(x => x.InitialPassword).MustBeValidPassword();
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("Display name is required.").MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Role).Must(r => RoleNames.All.Contains(r)).WithMessage("Select a valid role.");
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator() => RuleFor(x => x.NewPassword).MustBeValidPassword();
}
