using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Users.GetSetupStatus;

public sealed class GetSetupStatusHandler(IReadRepository<User> repository, SetupWindow window)
  : IQueryHandler<GetSetupStatusQuery, Result<SetupStatus>>
{
  public async ValueTask<Result<SetupStatus>> Handle(GetSetupStatusQuery query, CancellationToken cancellationToken)
  {
    if (await repository.AnyAsync(cancellationToken))
    {
      return SetupStatus.Done;
    }

    return window.IsOpen ? SetupStatus.Required : SetupStatus.TimedOut;
  }
}
