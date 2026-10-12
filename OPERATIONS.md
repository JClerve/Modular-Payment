# SettleCore backend operations

SettleCore is a modular .NET backend with separate Payments, Ledger and Reconciliation persistence contexts. Stripe PaymentIntents are the first provider. SDK types remain in Payments Infrastructure; application/domain contracts remain provider-neutral.

## Verify and run

Use the SDK pinned by `global.json` and a running Docker daemon for PostgreSQL integration tests.

```sh
dotnet restore SettleCore.slnx
dotnet build SettleCore.slnx --configuration Release --no-restore
dotnet test SettleCore.slnx --configuration Release --no-build --verbosity normal
```

Supply `ConnectionStrings__Payments`, `ConnectionStrings__Ledger` and `ConnectionStrings__Reconciliation` through private local process environment or a local secret store. Development connection strings are local examples. Production has no database defaults. Keep credentials out of tracked configuration and verification logs.

Apply all module migrations before starting the local flow or enabling workers. Use the infrastructure project as both project and startup project; each has its own design-time context factory. Always provide the intended connection explicitly so the design-time database is not selected accidentally.

```sh
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef database update --project src/SettleCore.Modules.Payments.Infrastructure --startup-project src/SettleCore.Modules.Payments.Infrastructure --context PaymentsDbContext --connection "$ConnectionStrings__Payments"
dotnet ef database update --project src/SettleCore.Modules.Ledger.Infrastructure --startup-project src/SettleCore.Modules.Ledger.Infrastructure --context LedgerDbContext --connection "$ConnectionStrings__Ledger"
dotnet ef database update --project src/SettleCore.Modules.Reconciliation.Infrastructure --startup-project src/SettleCore.Modules.Reconciliation.Infrastructure --context ReconciliationDbContext --connection "$ConnectionStrings__Reconciliation"
```

The newest Payments migration is `20261010105446_AddPaymentProviderCreationAttempts`. Earlier inbox, posting preparation and receipt retry migrations are also required. Migrations are not applied automatically at host startup.

`/health/live` checks process liveness without database dependencies. `/health/ready` returns 503 if any module database is unreachable or has pending migrations; 200 requires all three ready. Dependency checks have five-second timeouts and do not return connection details.

## Explicit provider configuration

Stripe and both workers are disabled by default. Configure each independently:

| Environment setting | Meaning |
|---|---|
| `Payments__Stripe__Enabled` | Enable Stripe creation, retrieval and signed ingress |
| `Payments__Stripe__ApiKey` | API key from the secret store |
| `Payments__Stripe__WebhookSecret` | Endpoint signing secret from the secret store |
| `Payments__Stripe__SignatureToleranceSeconds` | Explicit positive signature tolerance |
| `Payments__Stripe__IsLiveMode` | Explicit `true` or `false`; must match incoming/retrieved provider evidence |
| `PaymentProviderEventWorker__Enabled` | Poll and process durable provider receipts; requires enabled Stripe and explicit mode |
| `PaymentLedgerPostingWorker__Enabled` | Dispatch durable posting intents to Ledger |

Each worker has `BatchSize` (default 100), `PollIntervalMilliseconds` (default 5000) and `RetryDelaySeconds` (default 60) under its section. All must be positive. These are operational settings, not accounting policy. Enabling Stripe does not enable either worker.

## Payment flow

1. Create a local payment with `POST /payments` and explicit amount/currency.
2. Provision the intended Ledger accounts with `POST /ledger/accounts` using explicit `accountId`, `ledgerId` and `currency`. Identical replay returns 200; an existing account with a different ledger or currency returns 409. Currency is normalized; no ledger or account IDs are generated.
3. Submit `POST /payments/{paymentId}/ledger-posting/preparation` with explicit transaction, ledger, processor receivable, merchant payable and platform revenue account IDs and fee amount in minor units. Fee must be positive and below gross. Stored preparation is immutable; conflicting changes return 409. No fees, accounts or routing are inferred.
4. Call `POST /payments/{paymentId}/provider-payment`. Missing payment or disabled provider returns 404; missing preparation, completed/linked payment or expired creation attempt returns 409. The SDK receives stored amount/currency, identity metadata and `settlecore:payment:{paymentId:N}:create` as its idempotency key. Reference persistence must succeed before the response returns the client secret.
5. Use `GET /payments/{paymentId}/provider-payment` to recover details for an already-linked intent. It retrieves the stored reference, verifies identity/amount/currency/mode, and returns 409 without a secret on mismatch. Missing/unlinked/disabled returns 404. Creation and lookup responses containing secrets use `Cache-Control: no-store`. Neither provider observation marks a local payment succeeded.
6. Stripe sends `payment_intent.succeeded` to `POST /payments/webhooks/stripe` with its signature and exact raw body. Verified matching-mode evidence is durably stored before 200. Invalid signature/mode returns 400; conflicting replay returns 409; storage failure is never acknowledged as success.
7. The receipt worker correlates evidence with the stored payment/reference/amount/currency/mode and explicit preparation. It atomically writes local success, one posting intent, lifecycle audit and receipt processing time. Deferred/failed receipts retain durable retries.
8. The posting worker writes the balanced Ledger transaction, then acknowledges the intent. Observe `GET /payments/{paymentId}/ledger-posting` and `GET /ledger/transactions/{transactionId}`. Idempotent processing/replay preserves one transaction.

Manual HTTP success is disabled whenever Stripe is enabled. Provider-linked payments cannot use manual application completion. Client secrets are response-only: never persist them, log them or put them in URLs. All Payments, Ledger and Reconciliation endpoints require the local operator key by default.

## Creation retry recovery

The first creation attempt is recorded durably before contacting the provider. Matching retries do not reset its timestamp. Stripe composition uses a conservative 23-hour creation retry window; at or after that boundary, or for future-dated records, creation returns 409 without contacting Stripe.

If creation succeeded remotely but reference persistence failed, retry with the same local payment only inside that window. Afterward, locate the original Stripe PaymentIntent using the SettleCore identity metadata and trusted provider records, then POST `/payments/{paymentId}/provider-reference` with `provider: "stripe"` and its original `reference`. With Stripe enabled this path retrieves the supplied intent and verifies reference, identity, amount, currency and configured mode before durable attachment. A mismatch returns 409 with no attachment; a lookup failure does not attach. Matching replay is idempotent. Recovery never creates a new intent, resets the first attempt, or completes the local payment; use GET retrieval afterward and let signed success evidence authorize completion. If no unique original intent can be established, leave the payment blocked and investigate; absence from a search is not permission to recreate. Do not delete the attempt record, invent a new payment identity or blindly create a replacement to bypass the guard. A prior remote attempt made before this timestamp mechanism was deployed must be reconciled explicitly; the backend cannot infer its historical age.

Stripe v1 keys can be pruned after at least 24 hours; reuse after pruning may create another object. See [Stripe idempotent requests](https://docs.stripe.com/api/idempotent_requests) and [PaymentIntent reuse and client-secret handling](https://docs.stripe.com/payments/payment-intents).

## Acceptance boundary

The agreed local backend scope is verified with the actual SDK over isolated fake transport, real PostgreSQL, default-on operator controls, explicit account provisioning and safe original-intent recovery. The complete acceptance includes all module migrations/readiness and both workers. Real local HTTPS operator/manual-payment flow is also verified. See [acceptance evidence](docs/ACCEPTANCE.md) and [the reproducible demo](docs/LOCAL_DEMO.md).

External Stripe TEST-mode acceptance remains optional and unverified because credentials were not supplied. Customer/tenant identities, refunds/payouts, merchant routing/fee policy, frontend and deployment are outside this portfolio scope. No hosted rollout or live payment is claimed. Older checkpoint entries describe historical remaining work; the newest acceptance entry is authoritative for current scope.

The posting metrics meter is `SettleCore.Payments`, with `settlecore.payment_posting.attempts`, `.completed`, `.failures` and `.retry_scheduling_failures`. Receipt processing emits bounded operational logs with provider/event identity; raw webhook bodies and client secrets are not logged. Tracked `PROJECT_CHECKPOINT.md` provides durable continuity; automatic chat switching and account-wide memory editing are not claimed.

Receipt worker metrics use the same meter, with `settlecore.provider_receipts.attempts`, `.completed`, `.deferred`, `.failures` and `.retry_scheduling_failures`. They have no tags. Completion counts processing outcomes (including already-processed replays), not unique charges. Deferral means missing local prerequisites; exceptions and retry-write failures have separate counters. Host cancellation does not count as failure.

## Local operator access

Set `LocalApiAccess__OperatorKey` to a randomly generated secret of at least 32 characters in the process environment (for example, `openssl rand -hex 32`). Send it only in the `X-SettleCore-Operator-Key` header over HTTPS. Access is enabled by default, including Development; an absent/short key prevents startup. Missing, wrong, duplicated or plain-HTTP credentials return 401 before handler/database/provider work. Key comparison is fixed-time over SHA-256 hashes; responses and validation messages do not include the key.

All Payments, Ledger and Reconciliation routes share the operator authorization policy. This is a single-operator local portfolio backend, without customer identities or tenant roles. Health endpoints are public. The separately mapped Stripe webhook accepts no operator credential and instead authenticates its exact raw body with the Stripe signature. The integration test assembly explicitly disables access for existing isolated behavior tests; security and complete acceptance tests turn it on. An explicit `LocalApiAccess__Enabled=false` is allowed only in Development for isolated experiments; it is not the demo default. Do not share the key, put it in URLs or tracked files, enable HTTP access, or bind the local demo to a public interface.
