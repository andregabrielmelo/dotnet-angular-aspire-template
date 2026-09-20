namespace AppTemplate.Core.ValueObjects;

// Intentionally naive (checks for one '@', not first/last char) rather than a strict regex -
// mirrors FluentValidation's default EmailAddress() / ASP.NET Core's EmailAddressAttribute.
// A syntactically-stricter regex can't confirm the address is real anyway; Register already
// does that via an emailed confirmation link.
public class EmailAddress : ValueObject
{
    public string Value { get; private set; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");

        var atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex == value.Length - 1 || atIndex != value.LastIndexOf('@'))
            throw new ArgumentException("Invalid email format");

        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
