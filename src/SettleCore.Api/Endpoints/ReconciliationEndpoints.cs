using SettleCore.Modules.Reconciliation.Application.CreateReconciliation;
using SettleCore.Modules.Reconciliation.Application.GetReconciliationById;

namespace SettleCore.Api.Endpoints;

public static class ReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapReconciliationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/reconciliations",
                async Task<IResult> (
                    CreateReconciliationRequest request,
                    CreateReconciliationHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var result = await handler.HandleAsync(
                            new CreateReconciliationCommand(
                                request.ExpectedAmount,
                                request.ExpectedCurrency,
                                request.ActualAmount,
                                request.ActualCurrency),
                            cancellationToken);

                        return Results.Created(
                            $"/reconciliations/{result.ReconciliationId}",
                            result);
                    }
                    catch (ArgumentException exception)
                    {
                        var parameterName =
                            exception.ParamName ?? "reconciliation";

                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                [parameterName] = [exception.Message]
                            });
                    }
                })
            .WithName("CreateReconciliation")
            .Produces<CreateReconciliationResult>(
                StatusCodes.Status201Created)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest);

        endpoints.MapGet(
                "/reconciliations/{reconciliationId:guid}",
                async Task<IResult> (
                    Guid reconciliationId,
                    GetReconciliationByIdHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.HandleAsync(
                        new GetReconciliationByIdQuery(reconciliationId),
                        cancellationToken);

                    return result is null
                        ? Results.NotFound()
                        : Results.Ok(result);
                })
            .WithName("GetReconciliationById")
            .Produces<GetReconciliationByIdResult>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }
}

public sealed record CreateReconciliationRequest(
    decimal ExpectedAmount,
    string ExpectedCurrency,
    decimal ActualAmount,
    string ActualCurrency);
