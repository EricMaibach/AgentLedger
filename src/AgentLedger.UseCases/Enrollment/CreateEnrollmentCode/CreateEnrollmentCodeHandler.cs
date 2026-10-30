using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Enrollment.CreateEnrollmentCode;

// A one-time code for connecting a machine (ADR 0024). Only its hash is stored.
public sealed class CreateEnrollmentCodeHandler(IRepository<EnrollmentCode> codes, IReadRepository<User> users, TimeProvider clock)
  : ICommandHandler<CreateEnrollmentCodeCommand, Result<CreatedEnrollmentCode>>
{
  public async ValueTask<Result<CreatedEnrollmentCode>> Handle(CreateEnrollmentCodeCommand command, CancellationToken cancellationToken)
  {
    if (!UserId.TryFrom(command.OwnerId, out var ownerId) || await users.GetByIdAsync(ownerId, cancellationToken) is null)
    {
      return Result.NotFound();
    }

    var text = EnrollmentCodeText.New();
    var code = new EnrollmentCode(EnrollmentCodeId.From(Guid.CreateVersion7()), ownerId, EnrollmentCodeHash.Of(text.Value),
      clock.GetUtcNow());
    await codes.AddAsync(code, cancellationToken);
    return Result.Created(new CreatedEnrollmentCode(text.Value, code.ExpiresAt));
  }
}
