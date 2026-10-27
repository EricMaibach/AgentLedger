namespace AgentLedger.UseCases.Projections.RunProjections;

// One pass of the projection worker (ADR 0022). Lag: how long a raw row may take to commit after its updated_at was
// taken (docs/architecture.md, incremental processing).
public sealed record RunProjectionsCommand(TimeSpan Lag) : ICommand<Result<ProjectionRunSummary>>;

// Projected: sessions with new raw rows; Failed: sessions their adapter couldn't project; Rebuilt: sessions brought up
// to the current projection version.
public sealed record ProjectionRunSummary(int Projected, int Failed, int Rebuilt);
