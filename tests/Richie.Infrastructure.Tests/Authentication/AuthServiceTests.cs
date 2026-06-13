using Richie.Application.Authentication;
using Richie.Domain.Authentication;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Security;
using Richie.Infrastructure.Tests.Helpers;

namespace Richie.Infrastructure.Tests.Authentication;

public sealed class AuthServiceTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_db, new Argon2PasswordHasher(), _clock);
    }

    private static SignupRequest ValidSignup(string username = "alice", string password = "Sup3rSecret!") =>
        new("Alice Smith", username, password, 30, "London",
        [
            new SecurityAnswerInput(SecurityQuestion.MothersMaidenName, "Jones"),
            new SecurityAnswerInput(SecurityQuestion.CityOfBirth, "Leeds"),
            new SecurityAnswerInput(SecurityQuestion.FirstSchoolName, "St Marys"),
        ]);

    [Fact]
    public void Signup_CreatesUser()
    {
        Assert.False(_sut.AnyUserExists());

        SignupResult result = _sut.Signup(ValidSignup());

        Assert.True(result.Succeeded);
        Assert.True(_sut.AnyUserExists());
    }

    [Fact]
    public void Signup_DuplicateUsername_IsRejected_CaseInsensitively()
    {
        _sut.Signup(ValidSignup(username: "Alice"));

        SignupResult second = _sut.Signup(ValidSignup(username: "alice"));

        Assert.Equal(SignupStatus.UsernameTaken, second.Status);
    }

    [Theory]
    [InlineData("short")]            // < 8 chars
    public void Signup_WeakPassword_FailsValidation(string password)
    {
        SignupResult result = _sut.Signup(ValidSignup(password: password));

        Assert.Equal(SignupStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public void Login_WithCorrectPassword_Succeeds()
    {
        _sut.Signup(ValidSignup());

        LoginResult result = _sut.Login("alice", "Sup3rSecret!");

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.UserId);
    }

    [Fact]
    public void Login_ReportsFirstLogin_OnlyOnTheFirstSuccess()
    {
        _sut.Signup(ValidSignup());

        Assert.True(_sut.Login("alice", "Sup3rSecret!").IsFirstLogin);
        Assert.False(_sut.Login("alice", "Sup3rSecret!").IsFirstLogin);
    }

    [Fact]
    public void Login_WithWrongPassword_FailsWithInvalidCredentials()
    {
        _sut.Signup(ValidSignup());

        LoginResult result = _sut.Login("alice", "wrong-password");

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    [Fact]
    public void Login_LocksOut_AfterFiveFailedAttempts()
    {
        _sut.Signup(ValidSignup());

        for (int i = 0; i < 4; i++)
            Assert.Equal(LoginStatus.InvalidCredentials, _sut.Login("alice", "wrong").Status);

        Assert.Equal(LoginStatus.LockedOut, _sut.Login("alice", "wrong").Status);

        // Even the correct password is refused while locked.
        Assert.Equal(LoginStatus.LockedOut, _sut.Login("alice", "Sup3rSecret!").Status);
    }

    [Fact]
    public void Login_AutoUnlocks_AfterLockoutExpires()
    {
        _sut.Signup(ValidSignup());
        for (int i = 0; i < 5; i++)
            _sut.Login("alice", "wrong");

        _clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(LoginStatus.Success, _sut.Login("alice", "Sup3rSecret!").Status);
    }

    [Fact]
    public void GetSecurityQuestions_ReturnsChosen_OrEmptyForUnknownUser()
    {
        _sut.Signup(ValidSignup());

        IReadOnlyList<SecurityQuestion> questions = _sut.GetSecurityQuestions("alice");
        Assert.Equal(3, questions.Count);
        Assert.Contains(SecurityQuestion.CityOfBirth, questions);

        Assert.Empty(_sut.GetSecurityQuestions("nobody"));
    }

    [Fact]
    public void ResetPassword_WithCorrectAnswers_ChangesPassword()
    {
        _sut.Signup(ValidSignup());

        PasswordResetResult reset = _sut.ResetPassword("alice",
        [
            // Answers are case/whitespace-insensitive.
            new SecurityAnswerInput(SecurityQuestion.MothersMaidenName, "  jones "),
            new SecurityAnswerInput(SecurityQuestion.CityOfBirth, "LEEDS"),
            new SecurityAnswerInput(SecurityQuestion.FirstSchoolName, "St Marys"),
        ], "BrandNewP@ss1");

        Assert.True(reset.Succeeded);
        Assert.Equal(LoginStatus.Success, _sut.Login("alice", "BrandNewP@ss1").Status);
        Assert.Equal(LoginStatus.InvalidCredentials, _sut.Login("alice", "Sup3rSecret!").Status);
    }

    [Fact]
    public void ResetPassword_WithWrongAnswers_LocksAfterThreeAttempts()
    {
        _sut.Signup(ValidSignup());

        SecurityAnswerInput[] wrong =
        [
            new SecurityAnswerInput(SecurityQuestion.MothersMaidenName, "nope"),
            new SecurityAnswerInput(SecurityQuestion.CityOfBirth, "nope"),
            new SecurityAnswerInput(SecurityQuestion.FirstSchoolName, "nope"),
        ];

        Assert.Equal(PasswordResetStatus.IncorrectAnswers, _sut.ResetPassword("alice", wrong, "BrandNewP@ss1").Status);
        Assert.Equal(PasswordResetStatus.IncorrectAnswers, _sut.ResetPassword("alice", wrong, "BrandNewP@ss1").Status);
        Assert.Equal(PasswordResetStatus.LockedOut, _sut.ResetPassword("alice", wrong, "BrandNewP@ss1").Status);
    }

    [Fact]
    public void ChangePassword_WithCorrectCurrent_Succeeds()
    {
        _sut.Signup(ValidSignup());
        Guid userId = _sut.Login("alice", "Sup3rSecret!").UserId!.Value;

        ChangePasswordResult result = _sut.ChangePassword(userId, "Sup3rSecret!", "An0therP@ss");

        Assert.True(result.Succeeded);
        Assert.Equal(LoginStatus.Success, _sut.Login("alice", "An0therP@ss").Status);
    }

    public void Dispose() => _db.Dispose();
}
