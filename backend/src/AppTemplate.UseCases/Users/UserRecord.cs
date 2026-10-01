using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users;

public record UserRecord(int Id, string Name, string? PhoneNumber)
{
    public static UserRecord FromDto(UserDto user) =>
        new(user.Id.Value, user.Name.Value, FormatPhoneNumber(user.PhoneNumber));

    /// <summary><see cref="Core.ValueObjects.PhoneNumber.Unknown"/> means "none" - render it as null, not as blank text.</summary>
    private static string? FormatPhoneNumber(PhoneNumber? phoneNumber) =>
        phoneNumber is null || phoneNumber == Core.ValueObjects.PhoneNumber.Unknown
            ? null
            : phoneNumber.ToString();
}
