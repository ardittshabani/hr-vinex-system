using Vinex.HR.Contracts.Auth;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Contracts.Users;
using Vinex.HR.Contracts.Validators;

namespace Vinex.HR.Tests.Unit;

public class ValidatorTests
{
    [Fact]
    public void Login_requires_username_and_password()
    {
        var validator = new LoginRequestValidator();
        validator.Validate(new LoginRequest("", "")).IsValid.ShouldBeFalse();
        validator.Validate(new LoginRequest("admin", "secret")).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("short", false)]
    [InlineData("exactly10c", true)]
    [InlineData("a-much-longer-passphrase", true)]
    public void Passwords_need_at_least_ten_characters(string password, bool valid)
    {
        new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(password)).IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData("ab", false)]
    [InlineData("john.doe", true)]
    [InlineData("has space", false)]
    [InlineData("ok_name-1", true)]
    public void Usernames_are_restricted(string username, bool valid)
    {
        var request = new CreateUserRequest(username, "John Doe", null, RoleNames.Management, "a-valid-password");
        new CreateUserRequestValidator().Validate(request).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Create_user_rejects_unknown_role_and_bad_email()
    {
        var validator = new CreateUserRequestValidator();
        validator.Validate(new CreateUserRequest("john", "John", null, "Root", "a-valid-password")).IsValid.ShouldBeFalse();
        validator.Validate(new CreateUserRequest("john", "John", "not-an-email", RoleNames.Management, "a-valid-password")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Change_password_requires_a_different_new_password()
    {
        var validator = new ChangePasswordRequestValidator();
        validator.Validate(new ChangePasswordRequest("same-password-1", "same-password-1")).IsValid.ShouldBeFalse();
        validator.Validate(new ChangePasswordRequest("old-password-1", "new-password-2")).IsValid.ShouldBeTrue();
    }
}
