using Ardalis.Specification;

namespace AgentLedger.UnitTests.UseCases;

// Substitute repositories backed by a list, so handlers can find, add and update aggregates as they would for real.
internal static class RepositoryFake
{
  public static IRepository<T> Create<T, TId>(List<T> items, Func<T, TId> idOf, TId anyId)
    where T : class, IAggregateRoot where TId : notnull
  {
    var repository = Substitute.For<IRepository<T>>();
    SetUpReads(repository, items, idOf, anyId);
    repository.AddAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
      .Returns(call => { items.Add(call.Arg<T>()); return call.Arg<T>(); });
    return repository;
  }

  public static IReadRepository<T> CreateReadOnly<T, TId>(List<T> items, Func<T, TId> idOf, TId anyId)
    where T : class, IAggregateRoot where TId : notnull
  {
    var repository = Substitute.For<IReadRepository<T>>();
    SetUpReads(repository, items, idOf, anyId);
    return repository;
  }

  private static void SetUpReads<T, TId>(IReadRepositoryBase<T> repository, List<T> items, Func<T, TId> idOf, TId anyId)
    where T : class where TId : notnull
  {
    repository.AnyAsync(Arg.Any<CancellationToken>()).Returns(_ => items.Count > 0);
    repository.FirstOrDefaultAsync(Arg.Any<ISpecification<T>>(), Arg.Any<CancellationToken>())
      .Returns(call => call.Arg<ISpecification<T>>().Evaluate(items).FirstOrDefault());
    repository.ListAsync(Arg.Any<ISpecification<T>>(), Arg.Any<CancellationToken>())
      .Returns(call => call.Arg<ISpecification<T>>().Evaluate(items).ToList());
    // Generic GetByIdAsync<TId> doesn't match Arg.Any for a Vogen struct, so match any arguments instead.
    repository.GetByIdAsync(anyId, CancellationToken.None)
      .ReturnsForAnyArgs(call => items.FirstOrDefault(item => idOf(item).Equals(call.ArgAt<TId>(0))));
  }
}
