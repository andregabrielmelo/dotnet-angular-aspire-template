namespace AppTemplate.UnitTests.Core.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void Constructor_WithInvalidValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress(value));
    }

    [Fact]
    public void Constructor_WithValidValue_Succeeds()
    {
        var email = new EmailAddress("ada@example.com");

        Assert.Equal("ada@example.com", email.Value);
    }

    [Fact]
    public void Equals_SameValue_AreEqual()
    {
        Assert.Equal(new EmailAddress("ada@example.com"), new EmailAddress("ada@example.com"));
    }
}
