# Mutation Testing Summary

| File                                     | Score  | Killed | Survived | Timeout | No Coverage | Ignored | Compile Errors | Total Detected | Total Undetected | Total Mutants |
| ---------------------------------------- | ------ | ------ | -------- | ------- | ----------- | ------- | -------------- | -------------- | ---------------- | ------------- |
| Auditing\/AuditOptionsValidator.cs       | N\/A   | 0      | 0        | 0       | 0           | 28      | 2              | 0              | 0                | 30            |
| Auditing\/HttpAuditRequestInfo.cs        | N\/A   | 0      | 0        | 0       | 0           | 12      | 0              | 0              | 0                | 12            |
| Auth\/AuthOptions.cs                     | N\/A   | 0      | 0        | 0       | 0           | 77      | 5              | 0              | 0                | 82            |
| Auth\/AuthRateLimiting.cs                | N\/A   | 0      | 0        | 0       | 0           | 85      | 6              | 0              | 0                | 91            |
| Auth\/FugueClaimTypes.cs                 | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Auth\/HasPermissionAttribute.cs          | N\/A   | 0      | 0        | 0       | 0           | 8       | 1              | 0              | 0                | 9             |
| Auth\/JwtAccessTokenIssuer.cs            | N\/A   | 0      | 0        | 0       | 0           | 5       | 0              | 0              | 0                | 5             |
| Auth\/JwtBearerSetup.cs                  | N\/A   | 0      | 0        | 0       | 0           | 95      | 12             | 0              | 0                | 107           |
| Auth\/JwtCurrentUser.cs                  | N\/A   | 0      | 0        | 0       | 0           | 11      | 0              | 0              | 0                | 11            |
| Auth\/UnauthenticatedAuditLimiter.cs     | N\/A   | 0      | 0        | 0       | 0           | 32      | 0              | 0              | 0                | 32            |
| Contracts\/AuditRequests.cs              | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Contracts\/AuthRequests.cs               | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Contracts\/ClaimRequests.cs              | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Contracts\/CustomerRequests.cs           | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Contracts\/WebhookRequests.cs            | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/ApiControllerBase.cs        | N\/A   | 0      | 0        | 0       | 0           | 94      | 14             | 0              | 0                | 108           |
| Controllers\/AuditController.cs          | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/AuthController.cs           | N\/A   | 0      | 0        | 0       | 0           | 1       | 5              | 0              | 0                | 6             |
| Controllers\/ClaimsController.cs         | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/CustomersController.cs      | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/DevWebhookSinkController.cs | N\/A   | 0      | 0        | 0       | 0           | 8       | 0              | 0              | 0                | 8             |
| Controllers\/DiagnosticsController.cs    | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/GroupsController.cs         | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/MeController.cs             | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/ModulesController.cs        | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/OverridesController.cs      | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Controllers\/WebhooksController.cs       | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Correlation\/CorrelationContext.cs       | N\/A   | 0      | 0        | 0       | 0           | 14      | 0              | 0              | 0                | 14            |
| Correlation\/CorrelationHeaders.cs       | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Correlation\/CorrelationIdMiddleware.cs  | N\/A   | 0      | 0        | 0       | 0           | 30      | 0              | 0              | 0                | 30            |
| Cors\/ConsoleCors.cs                     | N\/A   | 0      | 0        | 0       | 0           | 46      | 8              | 0              | 0                | 54            |
| Http\/AuthProblems.cs                    | N\/A   | 0      | 0        | 0       | 0           | 17      | 1              | 0              | 0                | 18            |
| Http\/GlobalExceptionHandler.cs          | N\/A   | 0      | 0        | 0       | 0           | 40      | 2              | 0              | 0                | 42            |
| Http\/ProblemDetailsEnrichment.cs        | N\/A   | 0      | 0        | 0       | 0           | 10      | 0              | 0              | 0                | 10            |
| Http\/ProblemResult.cs                   | N\/A   | 0      | 0        | 0       | 0           | 3       | 0              | 0              | 0                | 3             |
| Http\/RequestBodyExtensions.cs           | N\/A   | 0      | 0        | 0       | 0           | 0       | 12             | 0              | 0                | 12            |
| Idempotency\/IdempotencyFilter.cs        | N\/A   | 0      | 0        | 0       | 0           | 79      | 72             | 0              | 0                | 151           |
| Idempotency\/IdempotencyKeyHeader.cs     | 98.04% | 49     | 1        | 1       | 0           | 6       | 7              | 50             | 1                | 64            |
| Idempotency\/IdempotentAttribute.cs      | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Logging\/LoggingExtensions.cs            | N\/A   | 0      | 0        | 0       | 0           | 20      | 4              | 0              | 0                | 24            |
| Modules\/DevelopmentOnlyAttribute.cs     | N\/A   | 0      | 0        | 0       | 0           | 3       | 0              | 0              | 0                | 3             |
| Modules\/ModuleAttribute.cs              | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Modules\/ModuleGateMiddleware.cs         | N\/A   | 0      | 0        | 0       | 0           | 21      | 0              | 0              | 0                | 21            |
| OpenApi\/DescriptionText.cs              | N\/A   | 0      | 0        | 0       | 0           | 34      | 0              | 0              | 0                | 34            |
| OpenApi\/FugueDocumentFilter.cs          | N\/A   | 0      | 0        | 0       | 0           | 99      | 1              | 0              | 0                | 100           |
| OpenApi\/FugueOperationFilter.cs         | N\/A   | 0      | 0        | 0       | 0           | 192     | 17             | 0              | 0                | 209           |
| OpenApi\/FugueSchemaFilter.cs            | N\/A   | 0      | 0        | 0       | 0           | 50      | 0              | 0              | 0                | 50            |
| OpenApi\/OpenApiExtensions.cs            | N\/A   | 0      | 0        | 0       | 0           | 37      | 1              | 0              | 0                | 38            |
| OpenApi\/ProblemResponseAttribute.cs     | N\/A   | 0      | 0        | 0       | 0           | 4       | 0              | 0              | 0                | 4             |
| OpenApi\/TypedIdSchemas.cs               | N\/A   | 0      | 0        | 0       | 0           | 13      | 0              | 0              | 0                | 13            |
| Program.cs                               | N\/A   | 0      | 0        | 0       | 0           | 60      | 0              | 0              | 0                | 60            |
| Seeding\/DevelopmentSeedHostedService.cs | N\/A   | 0      | 0        | 0       | 0           | 8       | 0              | 0              | 0                | 8             |

## The final mutation score is 98.04%

### *Coverage Thresholds: high:90 low:70 break:0*
