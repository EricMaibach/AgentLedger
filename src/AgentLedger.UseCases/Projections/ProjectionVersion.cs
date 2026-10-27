namespace AgentLedger.UseCases.Projections;

// The version of the projection logic (ADR 0022). Raise it whenever a change to an adapter or the model changes what
// gets projected: the worker then rebuilds every stored session in the background.
public static class ProjectionVersion
{
  // 2: session titles and step summaries. 3: a thinking block without text followed by one with text became one step,
  // wrongly (they are separate blocks). 4: each thinking block is its own step again.
  public const int Current = 4;
}
