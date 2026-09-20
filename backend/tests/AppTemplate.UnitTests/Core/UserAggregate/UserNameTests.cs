namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserNameTests
{
    [Fact]
    public void From_WithValidName_Succeeds()
    {
        var name = UserName.From("Ada Lovelace");

        Assert.Equal("Ada Lovelace", name.Value);
    }

    [Fact]
    public void From_WithEmptyName_Throws()
    {
        Assert.Throws<Vogen.ValueObjectValidationException>(() => UserName.From(string.Empty));
    }

    [Fact]
    public void From_WithNameLongerThanMaxLength_Throws()
    {
        var tooLong = new string('a', UserName.MaxLength + 1);

        Assert.Throws<Vogen.ValueObjectValidationException>(() => UserName.From(tooLong));
    }
}
