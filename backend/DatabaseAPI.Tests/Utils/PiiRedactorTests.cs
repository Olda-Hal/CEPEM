using Cepem.Shared;
using Xunit;

namespace DatabaseAPI.Tests.Utils;

public class PiiRedactorTests
{
    [Theory]
    [InlineData("jan.novak@example.com", "j***@example.com")]
    [InlineData("a@example.com", "***@example.com")]
    [InlineData("not-an-email", "***")]
    [InlineData(null, null)]
    public void MaskEmail_MasksLocalPart(string? input, string? expected)
    {
        Assert.Equal(expected, PiiRedactor.MaskEmail(input));
    }

    [Theory]
    [InlineData("Jan Novák", "J*** N***")]
    [InlineData("Eva", "E***")]
    [InlineData("", "")]
    public void MaskName_KeepsInitials(string input, string expected)
    {
        Assert.Equal(expected, PiiRedactor.MaskName(input));
    }

    [Theory]
    [InlineData("10.1.2.3", "10.1.*.*")]
    [InlineData("2001:db8:1234:5678::1", "2001:0db8:****")]
    [InlineData("garbage", "***")]
    public void MaskIpAddress_MasksHostBits(string input, string expected)
    {
        Assert.Equal(expected, PiiRedactor.MaskIpAddress(input));
    }

    [Fact]
    public void RedactIpAddresses_ReplacesEmbeddedAddresses()
    {
        Assert.Equal(
            "Request from 10.1.*.*",
            PiiRedactor.RedactIpAddresses("Request from 10.1.2.3"));
    }

    [Fact]
    public void MaskEmails_ReplacesEmbeddedAddresses()
    {
        Assert.Equal(
            "SMTP rejected j***@example.com",
            PiiRedactor.MaskEmails("SMTP rejected jan.novak@example.com"));
    }

    [Theory]
    [InlineData("/api/events/intake-form-links/abcdef1234567890", "/api/events/intake-form-links/abcd***")]
    [InlineData("/api/events/intake-form-links/abcdef1234567890/submit", "/api/events/intake-form-links/abcd***/submit")]
    [InlineData("/api/patients/42", "/api/patients/42")]
    public void RedactPath_RemovesIntakeBearerTokens(string input, string expected)
    {
        Assert.Equal(expected, PiiRedactor.RedactPath(input));
    }
}
