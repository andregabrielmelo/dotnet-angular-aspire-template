using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Core.Aggregates.UserAggregate;

public class User(UserName name, EmailAddress email, string password)
    : EntityBase<User, UserId>,
        IAggregateRoot
{
    public UserName Name { get; private set; } = name;
    public EmailAddress Email { get; private set; } = email;
    public string Password { get; private set; } = password;
    public PhoneNumber? PhoneNumber { get; private set; }

    public static User Create(UserName name, EmailAddress email, string password) =>
        new User(name, email, password);

    public User UpdateName(UserName newName)
    {
        if (Name == newName)
        {
            return this;
        }

        Name = newName;
        return this;
    }

    public User UpdatePhoneNumber(PhoneNumber newPhoneNumber)
    {
        PhoneNumber = newPhoneNumber;
        return this;
    }

    public User UpdatePassword(string newPassword)
    {
        Password = newPassword;
        return this;
    }
}
