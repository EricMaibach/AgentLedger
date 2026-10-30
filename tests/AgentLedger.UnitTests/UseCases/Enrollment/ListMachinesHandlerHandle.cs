using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UnitTests.UseCases.Users;
using AgentLedger.UseCases.Enrollment;
using AgentLedger.UseCases.Enrollment.ListMachines;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.Enrollment;

// Who sees which machines (ADR 0024): members their own, admins everyone's, as with keys (ADR 0019).
public sealed class ListMachinesHandlerHandle
{
  private readonly IEnrollmentQueryService _queries = Substitute.For<IEnrollmentQueryService>();
  private readonly User _member = new UserBuilder().WithUsername("member").WithIsAdmin(false).Build();
  private readonly User _admin = new UserBuilder().WithUsername("admin").WithIsAdmin(true).Build();

  private ValueTask<Result<IReadOnlyList<MachineListItem>>> ListAsync(Guid viewer) =>
    new ListMachinesHandler(UserRepositoryFake.CreateReadOnly([_member, _admin]), _queries)
      .Handle(new ListMachinesQuery(viewer), CancellationToken.None);

  [Fact]
  public async Task MembersSeeTheirOwn()
  {
    await ListAsync(_member.Id.Value);

    await _queries.Received().ListMachinesAsync(_member.Id, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task AdminsSeeEveryones()
  {
    await ListAsync(_admin.Id.Value);

    await _queries.Received().ListMachinesAsync(null, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task AnUnknownViewerIsNotFound()
  {
    (await ListAsync(Guid.NewGuid())).Status.ShouldBe(ResultStatus.NotFound);
  }
}
