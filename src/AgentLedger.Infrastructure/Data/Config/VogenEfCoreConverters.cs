using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
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
internal sealed partial class VogenEfCoreConverters;
