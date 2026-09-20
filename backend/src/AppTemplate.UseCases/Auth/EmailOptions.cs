namespace AppTemplate.UseCases.Auth;

/// <summary>
/// Bound from "Authentication:Email" config. Lives in UseCases (not Web), same reasoning as
/// FrontendOptions: Register/ForgotPassword handlers need it directly to send confirmation/
/// reset emails, and UseCases cannot depend on Web - Web binds this from config in its own
/// composition root and it flows down via IOptions&lt;EmailOptions&gt;.
/// </summary>
public class EmailOptions
{
    public string FromAddress { get; set; } = "noreply@apptemplate.local";
    public int ConfirmationTokenLifetimeHours { get; set; } = 24;
    public int ResetTokenLifetimeHours { get; set; } = 1;
}
