using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Unit.Auth;

public sealed class TestEmailServiceTests
{
    [Test]
    public async Task TestEmailService_Should_Treat_Email_Case_InsensitivelyAsync()
    {
        var service = new TestEmailService();

        await service.GenerateCodeAsync("User.Name@Example.Test");
        var code = service.GetCode("user.name@example.test");

        var isValid = await service.VerifyCodeAsync("USER.NAME@EXAMPLE.TEST", code);

        Assert.That(isValid, Is.True);
    }

    [Test]
    public async Task TestEmailService_Should_Invalidate_Code_After_Successful_VerificationAsync()
    {
        var service = new TestEmailService();

        await service.GenerateCodeAsync("single-use@example.test");
        var code = service.GetCode("single-use@example.test");

        var firstAttempt = await service.VerifyCodeAsync("single-use@example.test", code);
        var secondAttempt = await service.VerifyCodeAsync("single-use@example.test", code);

        Assert.Multiple(() =>
        {
            Assert.That(firstAttempt, Is.True);
            Assert.That(secondAttempt, Is.False);
        });
    }
}
