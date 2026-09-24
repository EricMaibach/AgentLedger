using AgentLedger.Core.AgentEventAggregate;
using Vogen;

namespace AgentLedger.Infrastructure.Data.Config;

// Vogen generates an EF value converter and a HasVogenConversion() extension for each listed type.
[EfCoreConverter<AgentEventId>]
internal sealed partial class VogenEfCoreConverters;
