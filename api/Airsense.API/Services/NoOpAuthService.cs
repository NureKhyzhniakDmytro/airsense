namespace Airsense.API.Services;

public class NoOpAuthService : IAuthService
{
    public Task<bool> SetIdAsync(string uid, int id) => Task.FromResult(true);
}
