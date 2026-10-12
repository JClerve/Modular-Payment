# Reproducible local demo

Run these commands from the repository root. The automated Stripe demonstration needs .NET from `global.json` and a running Docker daemon. The interactive examples also use `openssl`, `curl`, Python 3 and `jq`. Initial NuGet/container downloads may require network access; no external Stripe account or credential is used by automated acceptance.

## Fully local Stripe acceptance

```sh
dotnet restore SettleCore.slnx
dotnet build SettleCore.slnx --configuration Release --no-restore
dotnet test tests/SettleCore.IntegrationTests --configuration Release --no-build \
  --filter FullyQualifiedName~CreatedProviderPaymentCompletesOnlyFromSignedEvidenceAndPostsBalancedLedgerOnce \
  --verbosity normal
```

The test starts three isolated PostgreSQL containers, applies all module migrations and checks readiness/model snapshots. It enables local operator authentication, rejects an unauthenticated read, provisions three explicit accounts through concurrent API replays, prepares the caller-selected posting IDs/34 minor-unit fee, and creates a PaymentIntent using the real Stripe SDK over fake HTTP transport. The fake creation reports success, but the local payment remains Pending. A bad signed webhook is rejected; a valid signed receipt drives the receipt worker and posting worker into one balanced 1234 debit / 1200 merchant credit / 34 revenue credit transaction. Matching webhook replay remains idempotent. Authenticated reads and an explicit local reconciliation comparison complete the flow. The test cleans up its containers.

Run all provider creation/recovery and security cases with:

```sh
dotnet test tests/SettleCore.IntegrationTests --configuration Release --no-build \
  --filter 'FullyQualifiedName~CreateProviderPaymentEndpointTests|FullyQualifiedName~LocalApiAccessTests' \
  --verbosity normal
```

This also proves expired-orphan recovery retrieves the supplied original intent without creating another object or completing the payment. SDK adapter cases independently reject identity, amount, currency, reference and mode mismatches. Run the full README verification before calling a modified checkout accepted.

## Interactive operator API, without Stripe credentials

Use a fresh local demo database. Generate credentials into the current shell environment; do not enable shell tracing or print them.

```sh
export POSTGRES_PASSWORD="$(openssl rand -hex 24)"
export LocalApiAccess__OperatorKey="$(openssl rand -hex 32)"
export LocalApiAccess__Enabled=true
export Payments__Stripe__Enabled=false
export PaymentProviderEventWorker__Enabled=false
export PaymentLedgerPostingWorker__Enabled=true

docker run --detach --name settlecore-local \
  --publish 127.0.0.1:55432:5432 \
  --env POSTGRES_USER=settlecore --env POSTGRES_PASSWORD \
  --env POSTGRES_DB=settlecore_payments postgres:18-alpine
```

Wait until `docker exec settlecore-local pg_isready -U settlecore` reports ready, then create the other two databases (once per fresh container):

```sh
docker exec settlecore-local createdb -U settlecore settlecore_ledger
docker exec settlecore-local createdb -U settlecore settlecore_reconciliation
export ConnectionStrings__Payments="Host=localhost;Port=55432;Database=settlecore_payments;Username=settlecore;Password=$POSTGRES_PASSWORD"
export ConnectionStrings__Ledger="Host=localhost;Port=55432;Database=settlecore_ledger;Username=settlecore;Password=$POSTGRES_PASSWORD"
export ConnectionStrings__Reconciliation="Host=localhost;Port=55432;Database=settlecore_reconciliation;Username=settlecore;Password=$POSTGRES_PASSWORD"
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef database update --project src/SettleCore.Modules.Payments.Infrastructure --startup-project src/SettleCore.Modules.Payments.Infrastructure --context PaymentsDbContext --connection "$ConnectionStrings__Payments"
dotnet ef database update --project src/SettleCore.Modules.Ledger.Infrastructure --startup-project src/SettleCore.Modules.Ledger.Infrastructure --context LedgerDbContext --connection "$ConnectionStrings__Ledger"
dotnet ef database update --project src/SettleCore.Modules.Reconciliation.Infrastructure --startup-project src/SettleCore.Modules.Reconciliation.Infrastructure --context ReconciliationDbContext --connection "$ConnectionStrings__Reconciliation"
```

For HTTPS use the .NET development certificate. `dotnet dev-certs https --trust` may require your operating system's confirmation; it changes your local certificate trust. Alternatively configure a local certificate explicitly through Kestrel's certificate path/key settings. Start only the loopback HTTPS profile:

```sh
dotnet run --project src/SettleCore.Api --configuration Release --no-build --launch-profile https &
settlecore_api_pid=$!
```

In the same shell, define an operator request helper that passes the key through curl's standard input rather than embedding its value in the process arguments. The examples assume a trusted development certificate.

```sh
api() {
  curl --silent --show-error --fail --config - "$@" <<CURL
header = "X-SettleCore-Operator-Key: $LocalApiAccess__OperatorKey"
CURL
}
export SETTLECORE_URL=https://localhost:7220
curl --fail "$SETTLECORE_URL/health/live"
curl --fail "$SETTLECORE_URL/health/ready"
```

Both health checks should return 200 after startup/migrations. An unauthenticated `POST /payments` returns 401. Choose explicit demonstration IDs; these examples represent one selected ledger and three selected accounts, not an account-routing or fee policy:

```sh
ledger_id=11111111-1111-1111-1111-111111111111
processor_id=22222222-2222-2222-2222-222222222222
merchant_id=33333333-3333-3333-3333-333333333333
revenue_id=44444444-4444-4444-4444-444444444444
transaction_id=$(python3 -c 'import uuid; print(uuid.uuid4())')
for account_id in "$processor_id" "$merchant_id" "$revenue_id"; do
  api -H 'Content-Type: application/json' -d "{\"accountId\":\"$account_id\",\"ledgerId\":\"$ledger_id\",\"currency\":\"SGD\"}" "$SETTLECORE_URL/ledger/accounts"
done
payment_id=$(api -H 'Content-Type: application/json' -d '{"amount":12.34,"currency":"SGD"}' "$SETTLECORE_URL/payments" | jq -r .paymentId)
posting_input="{\"transactionId\":\"$transaction_id\",\"ledgerId\":\"$ledger_id\",\"processorReceivableAccountId\":\"$processor_id\",\"merchantPayableAccountId\":\"$merchant_id\",\"platformRevenueAccountId\":\"$revenue_id\",\"feeAmountMinorUnits\":34}"
api -H 'Content-Type: application/json' -d "$posting_input" "$SETTLECORE_URL/payments/$payment_id/succeed"
api "$SETTLECORE_URL/payments/$payment_id/ledger-posting"
api "$SETTLECORE_URL/ledger/transactions/$transaction_id"
```

Poll the posting resource until `status` is `Posted` and `postedAt` is present (default worker polling is five seconds). Ledger retrieval should show the same exact transaction ID and three balanced entries. Re-provisioning identical accounts returns 200; conflicting ledger/currency returns 409. The manual success route is deliberately unavailable when Stripe is enabled. This interactive demonstration makes no provider call and is separate from signed Stripe acceptance above.

Stop the host with `kill "$settlecore_api_pid"` and stop the demo container with `docker stop settlecore-local`. Keep its data for inspection; remove a disposable container only when you intentionally want to discard it. Unset the generated secrets/connection variables after the session. Reusing the named container requires its original password; do not regenerate it and assume the existing database changed.

## Optional external Stripe TEST-mode acceptance — not yet verified

This optional exercise requires your own Stripe test/sandbox credentials and Stripe CLI. It is external verification, not part of the credential-free local acceptance and not a deployment. Use only TEST-mode credentials; never confirm a live payment.

1. Follow [Stripe CLI webhook forwarding](https://docs.stripe.com/cli) and [Stripe testing](https://docs.stripe.com/testing). Start `stripe listen --events payment_intent.succeeded --forward-to https://localhost:7220/payments/webhooks/stripe`. If the development certificate is untrusted, use the CLI's documented `--skip-verify` option only for this loopback destination. Select an API version compatible with the pinned SDK (`StripeConfiguration.ApiVersion`); the decoder deliberately rejects incompatible event envelopes.
2. Supply the test API key and the listener's signing secret through private environment input (for example `read -rs`, followed by `export`), then set `Payments__Stripe__Enabled=true`, `Payments__Stripe__IsLiveMode=false`, `Payments__Stripe__SignatureToleranceSeconds=300` and `PaymentProviderEventWorker__Enabled=true`. Keep the posting worker and local operator authentication enabled. Restart the local host so it sees those settings. Do not record keys, signing secrets or client secrets in logs, screenshots or checkpoints.
3. Provision your selected accounts and create a new local payment as above. Submit `posting_input` to `/payments/{id}/ledger-posting/preparation`. Call `/payments/{id}/provider-payment`; capture only `providerReference.reference` into an `intent_id` variable instead of printing the client secret. Verify local status is still Pending.
4. Confirm **that exact original intent** with Stripe's test payment method, for example `stripe payment_intents confirm "$intent_id" --payment-method pm_card_visa --return-url https://localhost:7220`. Consult the [confirmation API](https://docs.stripe.com/api/payment_intents/confirm) if the selected test method requires additional parameters. Do not use `stripe trigger payment_intent.succeeded` as acceptance for this payment: it creates unrelated fixture objects lacking the local identity/reference correlation.
5. Observe the signed event arrive, local payment become Succeeded and posting become Posted with the chosen exact IDs/fee and one balanced transaction. Re-deliver the same provider event through Stripe's supported delivery tooling, then verify one durable receipt and one transaction. Record only object/event IDs, statuses, totals and times as evidence. Include how redelivery was performed; do not claim it if not exercised.
6. For recovery practice, find the original intent by exact `settlecore_payment_id` metadata and trusted provider records, then submit its reference to `/payments/{id}/provider-reference`. Mismatches must not attach. Never delete/reset an attempt or create a replacement to bypass expiry; investigate ambiguous/multiple candidates.

The external test has not been run in this completion. Required credentials were not supplied. Fake-transport success cannot prove account-specific webhook forwarding, Stripe account settings or external network behavior. See [Stripe idempotency retention](https://docs.stripe.com/api/idempotent_requests) and [PaymentIntent reuse/client-secret handling](https://docs.stripe.com/payments/payment-intents).
