namespace AgentLedger.UseCases.Projections;

// The version of the projection logic (ADR 0022). Raise it whenever a change to an adapter or the model changes what
// gets projected: the worker then rebuilds every stored session in the background.
public static class ProjectionVersion
{
  public const int Current = 2; // 2: session titles and step summaries
}
