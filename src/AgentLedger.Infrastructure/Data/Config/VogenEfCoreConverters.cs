using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.GitAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.Core.UserAggregate;
using Vogen;

namespace AgentLedger.Infrastructure.Data.Config;

// Vogen generates an EF value converter and a HasVogenConversion() extension for each listed type.
[EfCoreConverter<ReceiptId>]
[EfCoreConverter<AgentEventId>]
[EfCoreConverter<UserId>]
[EfCoreConverter<Username>]
[EfCoreConverter<PasswordHash>]
[EfCoreConverter<SecurityStamp>]
[EfCoreConverter<ApiKeyId>]
[EfCoreConverter<ApiKeySecretHash>]
[EfCoreConverter<TranscriptLineId>]
[EfCoreConverter<TranscriptFileId>]
[EfCoreConverter<TranscriptFileName>]
[EfCoreConverter<GitCommitId>]
[EfCoreConverter<CommitHash>]
[EfCoreConverter<GitTagObservationId>]
[EfCoreConverter<EnrollmentCodeId>]
[EfCoreConverter<EnrollmentCodeHash>]
internal sealed partial class VogenEfCoreConverters;
