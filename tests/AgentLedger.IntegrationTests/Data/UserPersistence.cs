using AgentLedger.Core.UserAggregate;
using AgentLedger.Core.UserAggregate.Specifications;
using AgentLedger.Infrastructure.Data;

namespace AgentLedger.IntegrationTests.Data;

[Collection(nameof(PostgresCollection))]
public sealed class UserPersistence(PostgresFixture db)
{
  private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task RoundTripsEveryProperty()
  {
    var original = NewUser();
    for (var i = 0; i < User.MaxFailedSignIns; i++)
    {
      original.RecordFailedSignIn(Now); // sets the failure count and the lockout
    }

    await SaveAsync(original);
    var loaded = await LoadAsync(original.Id);

    loaded.Id.ShouldBe(original.Id);
    loaded.Username.ShouldBe(original.Username);
    loaded.PasswordHash.ShouldBe(original.PasswordHash);
    loaded.SecurityStamp.ShouldBe(original.SecurityStamp);
    loaded.IsAdmin.ShouldBe(original.IsAdmin);
    loaded.FailedSignInCount.ShouldBe(User.MaxFailedSignIns);
    loaded.LockedUntil.ShouldBe(Now + User.LockoutDuration);
  }

  [Fact]
  public async Task RejectsASecondUserWithTheSameUsername()
  {
    var username = UniqueUsername();
    await SaveAsync(NewUser(username));

    // Usernames are stored lowercase, so a differently-cased name is the same username.
    await Assert.ThrowsAnyAsync<DbUpdateException>(() => SaveAsync(NewUser(username.ToUpperInvariant())));
  }

  [Fact]
  public async Task FindsAUserByUsername()
  {
    var username = UniqueUsername();
    var user = NewUser(username);
    await SaveAsync(user, NewUser());

    using var scope = db.Services.CreateScope();
    var found = await scope.ServiceProvider.GetRequiredService<IReadRepository<User>>()
      .SingleOrDefaultAsync(new UserByUsernameSpec(Username.From(username)));

    found.ShouldNotBeNull().Id.ShouldBe(user.Id);
  }

  private static string UniqueUsername() => $"user-{Guid.NewGuid():N}";

  private static User NewUser(string? username = null) =>
    new(UserId.From(Guid.CreateVersion7()), Username.From(username ?? UniqueUsername()),
      PasswordHash.From("AQAAAAIAAYagAAAAEhash"), SecurityStamp.From(Guid.CreateVersion7()), isAdmin: true);

  private async Task SaveAsync(params User[] users)
  {
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<User>>().AddRangeAsync(users);
  }

  private async Task<User> LoadAsync(UserId id)
  {
    using var scope = db.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<User>().AsNoTracking().SingleAsync(u => u.Id == id);
  }
}
