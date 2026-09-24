using AgentLedger.Core.AgentEventReceiptAggregate;
using Vogen;

namespace AgentLedger.Infrastructure.Data.Config;

// Vogen generates an EF value converter and a HasVogenConversion() extension for each listed type.
[EfCoreConverter<ReceiptId>]
[EfCoreConverter<AgentEventId>]
internal sealed partial class VogenEfCoreConverters;
