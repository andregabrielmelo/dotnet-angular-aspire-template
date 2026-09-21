namespace AppTemplate.UnitTests.Core.UserAggregate;

public class UserTests
{
    private static User CreateUser() =>
        User.Create(UserName.From("Ada Lovelace"), new EmailAddress("ada@example.com"));

    [Fact]
    public void Create_SetsNameAndEmail()
    {
        var user = CreateUser();

        Assert.Equal("Ada Lovelace", user.Name.Value);
        Assert.Equal("ada@example.com", user.Email.Value);
        Assert.Null(user.PhoneNumber);
    }

    [Fact]
    public void UpdateName_WithNewName_ChangesName()
    {
        var user = CreateUser();
        var newName = UserName.From("Grace Hopper");

        user.UpdateName(newName);

        Assert.Equal(newName, user.Name);
    }

    [Fact]
    public void UpdateName_WithSameName_ReturnsSameInstance()
    {
        var user = CreateUser();

        var result = user.UpdateName(user.Name);

        Assert.Same(user, result);
    }

    [Fact]
    public void UpdatePhoneNumber_SetsPhoneNumber()
    {
        var user = CreateUser();
        var phoneNumber = new PhoneNumber("+1", "5551234", null);

        user.UpdatePhoneNumber(phoneNumber);

        Assert.Equal(phoneNumber, user.PhoneNumber);
    }
}
