using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.Extensions;

public static class ResultExtensions
{
  /// <summary>
  /// Maps Result to TypedResults for endpoints that return Created, ValidationProblem, or ProblemHttpResult
  /// </summary>
  public static Results<Created<TResponse>, ValidationProblem, ProblemHttpResult> ToCreatedResult<TValue, TResponse>(
    this Result<TValue> result,
    Func<TValue, string> locationBuilder,
    Func<TValue, TResponse> mapResponse) =>
    CreatedOrProblem(result, value => locationBuilder(value), mapResponse);

  /// <summary>
  /// Created without a Location header, for created resources that have no URL of their own (yet).
  /// </summary>
  public static Results<Created<TResponse>, ValidationProblem, ProblemHttpResult> ToCreatedResult<TValue, TResponse>(
    this Result<TValue> result,
    Func<TValue, TResponse> mapResponse) =>
    CreatedOrProblem(result, _ => null, mapResponse);

  private static Results<Created<TResponse>, ValidationProblem, ProblemHttpResult> CreatedOrProblem<TValue, TResponse>(
    Result<TValue> result,
    Func<TValue, string?> locationBuilder,
    Func<TValue, TResponse> mapResponse)
  {
    return result.Status switch
    {
      ResultStatus.Ok or ResultStatus.Created => TypedResults.Created(locationBuilder(result.Value), mapResponse(result.Value)),
      ResultStatus.Invalid => ToValidationProblem(result),
      _ => TypedResults.Problem(
        title: "Create failed",
        detail: string.Join("; ", result.Errors),
        statusCode: StatusCodes.Status400BadRequest)
    };
  }

  /// <summary>
  /// Maps Result to TypedResults for GetById endpoints: Ok, NotFound, ValidationProblem (Invalid, errors keyed by field), or ProblemHttpResult
  /// </summary>
  public static Results<Ok<TResponse>, NotFound, ValidationProblem, ProblemHttpResult> ToGetByIdResult<TValue, TResponse>(
    this Result<TValue> result,
    Func<TValue, TResponse> mapResponse)
  {
    return ToOkOrNotFoundResult(result, mapResponse, "Get");
  }

  /// <summary>
  /// Maps Result to TypedResults for Update endpoints: Ok, NotFound, ValidationProblem, or ProblemHttpResult
  /// </summary>
  public static Results<Ok<TResponse>, NotFound, ValidationProblem, ProblemHttpResult> ToUpdateResult<TValue, TResponse>(
    this Result<TValue> result,
    Func<TValue, TResponse> mapResponse)
  {
    return ToOkOrNotFoundResult(result, mapResponse, "Update");
  }

  /// <summary>
  /// Maps Result to TypedResults for Delete endpoints that return NoContent, NotFound, or ProblemHttpResult
  /// </summary>
  public static Results<NoContent, NotFound, ProblemHttpResult> ToDeleteResult(
    this Result result)
  {
    return result.Status switch
    {
      ResultStatus.Ok => TypedResults.NoContent(),
      ResultStatus.NotFound => TypedResults.NotFound(),
      _ => TypedResults.Problem(
        title: "Delete failed",
        detail: string.Join("; ", result.Errors),
        statusCode: StatusCodes.Status400BadRequest)
    };
  }

  /// <summary>
  /// Private helper method for Ok/NotFound result patterns
  /// </summary>
  private static Results<Ok<TResponse>, NotFound, ValidationProblem, ProblemHttpResult> ToOkOrNotFoundResult<TValue, TResponse>(
    Result<TValue> result,
    Func<TValue, TResponse> mapResponse,
    string operationName)
  {
    return result.Status switch
    {
      ResultStatus.Ok => TypedResults.Ok(mapResponse(result.Value)),
      ResultStatus.NotFound => TypedResults.NotFound(),
      ResultStatus.Invalid => ToValidationProblem(result),
      _ => TypedResults.Problem(
        title: $"{operationName} failed",
        detail: string.Join("; ", result.Errors),
        statusCode: StatusCodes.Status400BadRequest)
    };
  }

  // A 400 with the validation errors keyed by field (identifier), e.g. {"errors":{"eventId":["..."]}}.
  private static ValidationProblem ToValidationProblem(Ardalis.Result.IResult result) =>
    TypedResults.ValidationProblem(
      result.ValidationErrors
        .GroupBy(e => e.Identifier ?? string.Empty)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

  /// <summary>
  /// Maps Result to TypedResults for endpoints that return Ok only (like List endpoints)
  /// </summary>
  public static Ok<TResponse> ToOkOnlyResult<TValue, TResponse>(
    this Result<TValue> result,
    Func<TValue, TResponse> mapResponse)
  {
    return TypedResults.Ok(mapResponse(result.Value));
  }
}
