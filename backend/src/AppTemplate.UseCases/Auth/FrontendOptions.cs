namespace AppTemplate.UseCases.Auth;

/// <summary>
/// Bound from "Frontend" config. Lives in UseCases (not Web, despite being a Web-composition
/// concern) because Register/ForgotPassword handlers need it to build confirmation/reset
/// links, and UseCases cannot depend on Web - Web binds this from config in its own
/// composition root and it flows down via IOptions&lt;FrontendOptions&gt;.
///
/// Under the Aspire AppHost, OptionConfigurations overrides BaseUrl with the frontend's
/// actual (dynamically assigned) address, since Aspire doesn't guarantee it a fixed port.
/// The default below only applies when running AppTemplate.Web outside the AppHost (e.g.
/// directly via `dotnet run`), where Aspire never injects that address.
/// </summary>
public class FrontendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:4200";
}
