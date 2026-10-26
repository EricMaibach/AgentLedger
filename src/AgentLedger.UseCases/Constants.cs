namespace AgentLedger.UseCases;

public static class Constants
{
  public const int DEFAULT_PAGE_SIZE = 50; // ADR 0016, docs/ui.md
  public const int MAX_PAGE_SIZE = 100;
  public const int MAX_TRANSCRIPT_BATCH_LINES = 1000; // ADR 0020
  public const int MAX_GIT_COMMITS_PER_BATCH = 200;   // ADR 0021
}
