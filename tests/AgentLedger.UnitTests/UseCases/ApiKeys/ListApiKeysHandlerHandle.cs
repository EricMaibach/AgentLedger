using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UnitTests.UseCases.Users;
using AgentLedger.UseCases.ApiKeys;
using AgentLedger.UseCases.ApiKeys.ListApiKeys;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.ApiKeys;

// Each user sees their own keys; admins see everyone's (ADR 0019).
public sealed class ListApiKeysHandlerHandle
{
  private readonly List<User> _users = [];
  private readonly IApiKeyQueryService _queries = Substitute.For<IApiKeyQueryService>();
  private readonly ListApiKeysHandler _handler;

  public ListApiKeysHandlerHandle()
  {
    _queries.ListAsync(Arg.Any<UserId?>(), Arg.Any<CancellationToken>()).Returns([]);
    _handler = new ListApiKeysHandler(UserRepositoryFake.CreateReadOnly(_users), _queries);
  }

  [Fact]
  public async Task ShowsAMemberOnlyTheirOwnKeys()
  {
    var member = new UserBuilder().WithIsAdmin(false).Build();
    _users.Add(member);

    (await _handler.Handle(new ListApiKeysQuery(member.Id.Value), CancellationToken.None)).IsSuccess.ShouldBeTrue();

    await _queries.Received(1).ListAsync(member.Id, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task ShowsAnAdminEveryonesKeys()
  {
    var admin = new UserBuilder().WithIsAdmin(true).Build();
    _users.Add(admin);

    await _handler.Handle(new ListApiKeysQuery(admin.Id.Value), CancellationToken.None);

    await _queries.Received(1).ListAsync(null, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task IsNotFoundForAnUnknownViewer()
  {
    (await _handler.Handle(new ListApiKeysQuery(Guid.CreateVersion7()), CancellationToken.None)).Status.ShouldBe(ResultStatus.NotFound);
  }
}
