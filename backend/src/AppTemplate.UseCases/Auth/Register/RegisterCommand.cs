using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.Interfaces;
using AppTemplate.Core.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AppTemplate.UseCases.Auth.Register;

public record RegisterCommand(
    UserName Name,
    EmailAddress Email,
    string Password,
    string? PhoneNumber
) : ICommand<Result<UserId>>;

public class RegisterHandler(
    IRepository<User> _userRepository,
    IPasswordHasher<User> _passwordHasher,
    IEmailSender _emailSender,
    IOptions<FrontendOptions> _frontendOptions,
    IOptions<EmailOptions> _emailOptions
) : ICommandHandler<RegisterCommand, Result<UserId>>
{
    public async ValueTask<Result<UserId>> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken
    )
    {
        var existingUser = await _userRepository.FirstOrDefaultAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );
        if (existingUser is not null)
        {
            return Result<UserId>.Invalid(
                new ValidationError(nameof(command.Email), "Email is already registered")
            );
        }

        var hashedPassword = _passwordHasher.HashPassword(null!, command.Password);
        var newUser = User.Create(command.Name, command.Email, hashedPassword);
        if (!string.IsNullOrEmpty(command.PhoneNumber))
        {
            newUser.UpdatePhoneNumber(new PhoneNumber("+1", command.PhoneNumber, string.Empty));
        }

        var rawToken = TokenHasher.GenerateRawToken();
        newUser.IssueToken(
            UserTokenPurpose.EmailConfirmation,
            TokenHasher.Hash(rawToken),
            DateTimeOffset.UtcNow.Add(
                TimeSpan.FromHours(_emailOptions.Value.ConfirmationTokenLifetimeHours)
            )
        );

        var createdUser = await _userRepository.AddAsync(newUser, cancellationToken);

        var confirmationLink =
            $"{_frontendOptions.Value.BaseUrl}/auth/confirm-email?token={rawToken}";
        await _emailSender.SendEmailAsync(
            command.Email.Value,
            _emailOptions.Value.FromAddress,
            "Confirm your email",
            $"Welcome! Please confirm your email by visiting: {confirmationLink}"
        );

        return createdUser.Id;
    }
}
