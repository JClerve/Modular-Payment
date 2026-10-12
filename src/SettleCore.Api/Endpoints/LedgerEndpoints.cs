using SettleCore.Modules.Ledger.Application.ProvisionLedgerAccount;
using Microsoft.AspNetCore.Mvc;
using SettleCore.Modules.Ledger.Application.GetLedgerTransactionById;
using SettleCore.Modules.Ledger.Application.PostLedgerTransaction;
using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Api.Endpoints;

public static class LedgerEndpoints
{
    public static IEndpointRouteBuilder MapLedgerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ledger/accounts", async Task<IResult> (
            ProvisionLedgerAccountCommand request, ProvisionLedgerAccountHandler handler, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await handler.HandleAsync(request, cancellationToken)); }
            catch (LedgerAccountConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                { [exception.ParamName ?? "account"] = [exception.Message] });
            }
        })
        .WithName("ProvisionLedgerAccount")
        .Produces<ProvisionLedgerAccountResult>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status409Conflict);

        endpoints.MapPost(
                "/ledger/transactions",
                async Task<IResult> (
                    PostLedgerTransactionRequest request,
                    PostLedgerTransactionHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        if (request.Entries is null ||
                            request.Entries.Any(entry => entry is null))
                        {
                            return Results.ValidationProblem(
                                new Dictionary<string, string[]>
                                {
                                    ["entries"] =
                                    [
                                        "Ledger entries must be provided and must not contain null values."
                                    ]
                                });
                        }

                        var command = new PostLedgerTransactionCommand(
                            request.TransactionId,
                            request.LedgerId,
                            request.Entries
                                .Select(entry =>
                                    new PostLedgerTransactionEntry(
                                        entry.AccountId,
                                        entry.Currency,
                                        entry.Direction,
                                        entry.AmountMinorUnits))
                                .ToArray());

                        var result = await handler.HandleAsync(
                            command,
                            cancellationToken);

                        return Results.Created(
                            $"/ledger/transactions/{result.TransactionId}",
                            result);
                    }
                    catch (LedgerTransactionConflictException exception)
                    {
                        return Results.Conflict(new { error = exception.Message });
                    }
                    catch (ArgumentException exception)
                    {
                        var parameterName =
                            exception.ParamName ?? "transaction";

                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                [parameterName] = [exception.Message]
                            });
                    }
                })
            .WithName("PostLedgerTransaction")
            .Produces<PostLedgerTransactionResult>(
                StatusCodes.Status201Created)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        endpoints.MapGet(
                "/ledger/transactions/{transactionId:guid}",
                async Task<IResult> (
                    Guid transactionId,
                    [FromServices] GetLedgerTransactionByIdHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.HandleAsync(
                        new GetLedgerTransactionByIdQuery(
                            transactionId),
                        cancellationToken);

                    return result is null
                        ? Results.NotFound()
                        : Results.Ok(result);
                })
            .WithName("GetLedgerTransactionById")
            .Produces<GetLedgerTransactionByIdResult>(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status404NotFound);

        return endpoints;
    }
}

public sealed record PostLedgerTransactionRequest(
    Guid TransactionId,
    Guid LedgerId,
    IReadOnlyList<PostLedgerTransactionEntryRequest> Entries);

public sealed record PostLedgerTransactionEntryRequest(
    Guid AccountId,
    string Currency,
    LedgerDirection Direction,
    long AmountMinorUnits);
