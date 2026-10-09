namespace AppTemplate.UseCases.Users.SyncProfiles;

/// <returns>How many users were updated.</returns>
public sealed record SyncUserProfilesCommand : ICommand<Result<int>>;
