using SettleCore.Modules.Payments.Application.RecoverProviderPayment;
using Microsoft.Extensions.Options;
using SettleCore.Modules.Payments.Application.GetProviderPayment;
using SettleCore.Modules.Payments.Application.CreateProviderPayment;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;
using SettleCore.Modules.Payments.Application.AttachProviderReference;
using SettleCore.Modules.Payments.Application.PreparePaymentLedgerPosting;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.CreatePayment;
using SettleCore.Modules.Payments.Application.GetPaymentLedgerPosting;
using SettleCore.Modules.Payments.Application.GetPaymentById;
using SettleCore.Modules.Payments.Application.GetPaymentByProviderReference;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;

namespace SettleCore.Api.Endpoints;

public static class PaymentsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/payments",
                async Task<IResult> (
                    CreatePaymentRequest request,
                    CreatePaymentHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var result = await handler.HandleAsync(
                            new CreatePaymentCommand(
                                request.Amount,
                                request.Currency),
                            cancellationToken);

                        return Results.Created(
                            $"/payments/{result.PaymentId}",
                            result);
                    }
                    catch (ArgumentException exception)
                    {
                        var parameterName =
                            exception.ParamName ?? "payment";

                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                [parameterName] =
                                [
                                    exception.Message
                                ]
                            });
                    }
                })
            .WithName("CreatePayment")
            .Produces<CreatePaymentResult>(
                StatusCodes.Status201Created)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest);

        endpoints.MapGet(
                "/payments/by-provider-reference",
                async Task<IResult> (
                    string provider,
                    string reference,
                    GetPaymentByProviderReferenceHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var result = await handler.HandleAsync(
                            new GetPaymentByProviderReferenceQuery(
                                provider,
                                reference),
                            cancellationToken);

                        return result is null
                            ? Results.NotFound()
                            : Results.Ok(result);
                    }
                    catch (ArgumentException exception)
                    {
                        var parameterName =
                            exception.ParamName ?? "providerReference";

                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                [parameterName] =
                                [
                                    exception.Message
                                ]
                            });
                    }
                })
            .WithName("GetPaymentByProviderReference")
            .Produces<GetPaymentByProviderReferenceResult>(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status404NotFound)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest);

        endpoints.MapGet(
                "/payments/{paymentId:guid}",
                async Task<IResult> (
                    Guid paymentId,
                    GetPaymentByIdHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var result = await handler.HandleAsync(
                            new GetPaymentByIdQuery(paymentId),
                            cancellationToken);

                        return result is null
                            ? Results.NotFound()
                            : Results.Ok(result);
                    }
                    catch (ArgumentException exception)
                    {
                        var parameterName =
                            exception.ParamName ?? "paymentId";

                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                [parameterName] =
                                [
                                    exception.Message
                                ]
                            });
                    }
                })
            .WithName("GetPaymentById")
            .Produces<GetPaymentByIdResult>(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status404NotFound)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest);

        endpoints.MapGet(
                "/payments/{paymentId:guid}/ledger-posting",
                async Task<IResult> (
                    Guid paymentId,
                    GetPaymentLedgerPostingHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var result = await handler.HandleAsync(
                            new GetPaymentLedgerPostingQuery(paymentId), cancellationToken);
                        return result is null ? Results.NotFound() : Results.Ok(result);
                    }
                    catch (ArgumentException exception)
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            [exception.ParamName ?? "paymentId"] = [exception.Message]
                        });
                    }
                })
            .WithName("GetPaymentLedgerPosting")
            .Produces<GetPaymentLedgerPostingResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        endpoints.MapPost(
                "/payments/{paymentId:guid}/succeed",
                async Task<IResult> (
                    Guid paymentId,
                    PaymentLedgerPostingInput postingInput,
                    RecordPaymentSuccessHandler handler,
                    IOptions<StripePaymentOptions> providerOptions,
                    CancellationToken cancellationToken) =>
                {
                    if (providerOptions.Value.Enabled)
                    {
                        return Results.Conflict(new { error = "Enabled provider payments require verified provider success evidence." });
                    }
                    try
                    {
                        var result = await handler.HandleAsync(
                            new RecordPaymentSuccessCommand(paymentId, postingInput),
                            cancellationToken);

                        return result is null
                            ? Results.NotFound()
                            : Results.Ok(result);
                    }
                    catch (ArgumentException exception)
                    {
                        var parameterName =
                            exception.ParamName ?? "paymentId";

                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                [parameterName] =
                                [
                                    exception.Message
                                ]
                            });
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Results.Conflict(
                            new
                            {
                                error = exception.Message
                            });
                    }
                })
            .WithName("MarkPaymentSucceeded")
            .Produces<MarkPaymentSucceededResult>(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status404NotFound)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest)
            .Produces(
                StatusCodes.Status409Conflict);

        endpoints.MapPost(
                "/payments/{paymentId:guid}/provider-reference",
                async Task<IResult> (
                    Guid paymentId,
                    AttachProviderReferenceRequest request,
                    AttachProviderReferenceHandler handler,
                    IOptions<StripePaymentOptions> options, IServiceProvider services,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var command = new AttachProviderReferenceCommand(paymentId, request.Provider, request.Reference);
                        var result = options.Value.Enabled
                            ? await services.GetRequiredService<RecoverProviderPaymentHandler>().HandleAsync(command, cancellationToken)
                            : await handler.HandleAsync(command, cancellationToken);

                        return result is null
                            ? Results.NotFound()
                            : Results.Ok(result);
                    }
                    catch (ArgumentException exception)
                    {
                        var parameterName =
                            exception.ParamName ?? "providerReference";

                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                [parameterName] =
                                [
                                    exception.Message
                                ]
                            });
                    }
                    catch (ProviderPaymentReferenceConflictException exception)
                    {
                        return Results.Conflict(
                            new
                            {
                                error = exception.Message
                            });
                    }
                    catch (InvalidOperationException) { return Results.Conflict(); }
                })
            .WithName("AttachProviderReference")
            .Produces<AttachProviderReferenceResult>(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status404NotFound)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest)
            .Produces(
                StatusCodes.Status409Conflict);

        endpoints.MapPost("/payments/{paymentId:guid}/ledger-posting/preparation", async Task<IResult> (
            Guid paymentId, PaymentLedgerPostingInput input, PreparePaymentLedgerPostingHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await handler.HandleAsync(new PreparePaymentLedgerPostingCommand(paymentId, input), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [exception.ParamName ?? "postingInput"] = [exception.Message]
                });
            }
            catch (PaymentLedgerPostingIntentConflictException)
            {
                return Results.Conflict();
            }
        })
        .WithName("PreparePaymentLedgerPosting")
        .Produces<PaymentLedgerPostingRequest>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        endpoints.MapPost("/payments/{paymentId:guid}/provider-payment", async Task<IResult> (
            Guid paymentId, HttpResponse response, IOptions<StripePaymentOptions> options, IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            if (!options.Value.Enabled) return Results.NotFound();
            try
            {
                var handler = services.GetRequiredService<CreateProviderPaymentHandler>();
                var result = await handler.HandleAsync(paymentId, cancellationToken);
                response.Headers.CacheControl = "no-store";
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [exception.ParamName ?? "paymentId"] = [exception.Message]
                });
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
            catch (ProviderPaymentReferenceConflictException)
            {
                return Results.Conflict();
            }
        })
        .WithName("CreateProviderPayment")
        .Produces<CreateProviderPaymentResult>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        endpoints.MapGet("/payments/{paymentId:guid}/provider-payment", async Task<IResult> (
            Guid paymentId, HttpResponse response, IOptions<StripePaymentOptions> options,
            IServiceProvider services, CancellationToken cancellationToken) =>
        {
            if (!options.Value.Enabled) return Results.NotFound();
            response.Headers.CacheControl = "no-store";
            try
            {
                var handler = services.GetRequiredService<GetProviderPaymentHandler>();
                var result = await handler.HandleAsync(paymentId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [exception.ParamName ?? "paymentId"] = [exception.Message]
                });
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
        })
        .WithName("GetProviderPayment")
        .Produces<CreateProviderPaymentResult>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return endpoints;
    }
}

public sealed record CreatePaymentRequest(
    decimal Amount,
    string Currency);

public sealed record AttachProviderReferenceRequest(
    string Provider,
    string Reference);
