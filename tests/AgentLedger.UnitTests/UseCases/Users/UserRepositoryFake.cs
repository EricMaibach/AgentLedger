using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UnitTests.UseCases.Users;

internal static class UserRepositoryFake
{
  private static readonly UserId AnyId = UserId.From(Guid.CreateVersion7());

  public static IRepository<User> Create(List<User> users) => RepositoryFake.Create(users, u => u.Id, AnyId);

  public static IReadRepository<User> CreateReadOnly(List<User> users) => RepositoryFake.CreateReadOnly(users, u => u.Id, AnyId);
}
