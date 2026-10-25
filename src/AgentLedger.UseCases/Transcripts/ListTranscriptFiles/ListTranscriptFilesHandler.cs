namespace AgentLedger.UseCases.Transcripts.ListTranscriptFiles;

public sealed class ListTranscriptFilesHandler(ITranscriptQueryService queries)
  : IQueryHandler<ListTranscriptFilesQuery, Result<IReadOnlyList<TranscriptFileItem>>>
{
  public async ValueTask<Result<IReadOnlyList<TranscriptFileItem>>> Handle(ListTranscriptFilesQuery query, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(query.NativeSessionId))
    {
      return Result.Invalid(new ValidationError("session", "A session ID is required."));
    }

    return Result.Success(await queries.ListFilesAsync(query.NativeSessionId, cancellationToken));
  }
}
