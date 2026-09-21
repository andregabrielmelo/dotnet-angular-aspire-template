using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Core.Aggregates.UserAggregate;

public class User(UserName name, EmailAddress email) : EntityBase<User, UserId>, IAggregateRoot
{
    public UserName Name { get; private set; } = name;
    public EmailAddress Email { get; private set; } = email;
    public PhoneNumber? PhoneNumber { get; private set; }

    public static User Create(UserName name, EmailAddress email) => new User(name, email);

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
}
