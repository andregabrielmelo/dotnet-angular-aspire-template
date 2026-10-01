using AppTemplate.UseCases.Users;

namespace AppTemplate.UnitTests.UseCases.Users;

public class UserRecordTests
{
    private static UserDto Dto(PhoneNumber? phoneNumber) =>
        new(UserId.From(7), UserName.From("Ada Lovelace"), phoneNumber);

    [Fact]
    public void FromDto_FormatsThePhoneNumber()
    {
        var record = UserRecord.FromDto(Dto(new PhoneNumber("+55", "11 98765 4321", "12")));

        Assert.Equal(new UserRecord(7, "Ada Lovelace", "+55 11 98765 4321 x12"), record);
    }

    [Fact]
    public void FromDto_WithoutPhoneNumber_ReturnsNull()
    {
        Assert.Null(UserRecord.FromDto(Dto(null)).PhoneNumber);
        Assert.Null(UserRecord.FromDto(Dto(PhoneNumber.Unknown)).PhoneNumber);
    }
}
