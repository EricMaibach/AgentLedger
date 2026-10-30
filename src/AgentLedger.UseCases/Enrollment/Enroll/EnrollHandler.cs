using System.Globalization;
using System.Security.Cryptography;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.EnrollmentAggregate.Specifications;

namespace AgentLedger.UseCases.Enrollment.Enroll;

// Exchanges a one-time code for a new ingest key owned by the code's creator (ADR 0024). Every unusable code gets
// the same answer, so it doesn't reveal whether a guessed code exists.
public sealed class EnrollHandler(IReadRepository<EnrollmentCode> codes, IEnrollmentStore store, TimeProvider clock)
  : ICommandHandler<EnrollCommand, Result<EnrolledMachine>>
{
  public const string UnusableCode = "This code isn't valid: it may have been used or have expired. Create a new one.";

  public async ValueTask<Result<EnrolledMachine>> Handle(EnrollCommand command, CancellationToken cancellationToken)
  {
    var host = command.Host?.Trim();
    if (string.IsNullOrEmpty(host) || host.Length > EnrollmentCode.MaxHostLength)
    {
      return Result.Invalid(new ValidationError("host", $"A host name (up to {EnrollmentCode.MaxHostLength} characters) is required."));
    }

    var now = clock.GetUtcNow();
    var code = string.IsNullOrWhiteSpace(command.Code)
      ? null
      : await codes.FirstOrDefaultAsync(new EnrollmentCodeByHashSpec(EnrollmentCodeHash.Of(command.Code)), cancellationToken);
    if (code is null || !code.IsUsable(now))
    {
      return Unusable();
    }

    var id = ApiKeyId.From(Guid.CreateVersion7());
    var token = ApiKeyToken.Create(ApiKeyScope.Ingest, id, RandomNumberGenerator.GetBytes(ApiKeyToken.SecretBytes));
    var name = $"{host} (connected {now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})";
    var key = new ApiKey(id, code.OwnerId, name, ApiKeyScope.Ingest, ApiKeySecretHash.Of(token.Secret), now, expiresAt: null);
    code.Use(now, host, id);

    return await store.RedeemAsync(code, key, cancellationToken)
      ? Result.Success(new EnrolledMachine(id.Value, name, token.Value))
      : Unusable();
  }

  private static Result<EnrolledMachine> Unusable() => Result.Invalid(new ValidationError("code", UnusableCode));
}
