using AgentLedger.Core.UserAggregate;
using Ardalis.Specification;

namespace AgentLedger.UnitTests.UseCases.Users;

// Substitute repositories backed by a list, so handlers can find, add and update users as they would for real.
internal static class UserRepositoryFake
{
  public static IRepository<User> Create(List<User> users)
  {
    var repository = Substitute.For<IRepository<User>>();
    SetUpReads(repository, users);
    repository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
      .Returns(call => { users.Add(call.Arg<User>()); return call.Arg<User>(); });
    return repository;
  }

  public static IReadRepository<User> CreateReadOnly(List<User> users)
  {
    var repository = Substitute.For<IReadRepository<User>>();
    SetUpReads(repository, users);
    return repository;
  }

  private static void SetUpReads(IReadRepositoryBase<User> repository, List<User> users)
  {
    repository.AnyAsync(Arg.Any<CancellationToken>()).Returns(_ => users.Count > 0);
    repository.FirstOrDefaultAsync(Arg.Any<ISpecification<User>>(), Arg.Any<CancellationToken>())
      .Returns(call => call.Arg<ISpecification<User>>().Evaluate(users).FirstOrDefault());
    // Generic GetByIdAsync<TId> doesn't match Arg.Any for a Vogen struct, so match any arguments instead.
    repository.GetByIdAsync(UserId.From(Guid.CreateVersion7()), CancellationToken.None)
      .ReturnsForAnyArgs(call => users.FirstOrDefault(u => u.Id == call.ArgAt<UserId>(0)));
  }
}
