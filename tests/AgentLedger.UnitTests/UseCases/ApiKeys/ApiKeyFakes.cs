using AgentLedger.Core.ApiKeyAggregate;

namespace AgentLedger.UnitTests.UseCases.ApiKeys;

internal static class ApiKeyRepositoryFake
{
  private static readonly ApiKeyId AnyId = ApiKeyId.From(Guid.CreateVersion7());

  public static IRepository<ApiKey> Create(List<ApiKey> keys) => RepositoryFake.Create(keys, k => k.Id, AnyId);
}
