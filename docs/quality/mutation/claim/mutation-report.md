# Mutation Testing Summary

| File                                 | Score  | Killed | Survived | Timeout | No Coverage | Ignored | Compile Errors | Total Detected | Total Undetected | Total Mutants |
| ------------------------------------ | ------ | ------ | -------- | ------- | ----------- | ------- | -------------- | -------------- | ---------------- | ------------- |
| Abstractions\/IAuditable.cs          | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Abstractions\/IAuditStamp.cs         | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Abstractions\/ICurrentUser.cs        | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Abstractions\/ISoftDeletable.cs      | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Audit\/AuditAttributes.cs            | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Audit\/AuditLog.cs                   | N\/A   | 0      | 0        | 0       | 0           | 1       | 0              | 0              | 0                | 1             |
| Claims\/Claim.cs                     | 94.37% | 67     | 4        | 0       | 0           | 13      | 9              | 67             | 4                | 93            |
| Claims\/ClaimEvents.cs               | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Claims\/ClaimInfoRequest.cs          | N\/A   | 0      | 0        | 0       | 0           | 6       | 0              | 0              | 0                | 6             |
| Claims\/ClaimTimelineEntry.cs        | N\/A   | 0      | 0        | 0       | 0           | 5       | 0              | 0              | 0                | 5             |
| Claims\/ClaimTypes.cs                | N\/A   | 0      | 0        | 0       | 0           | 11      | 1              | 0              | 0                | 12            |
| Customers\/Customer.cs               | N\/A   | 0      | 0        | 0       | 0           | 34      | 0              | 0              | 0                | 34            |
| Customers\/CustomerEvents.cs         | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Customers\/CustomerStatus.cs         | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Events\/IDomainEvent.cs              | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Idempotency\/IdempotencyRecord.cs    | N\/A   | 0      | 0        | 0       | 0           | 4       | 0              | 0              | 0                | 4             |
| Identity\/AccessAudit.cs             | N\/A   | 0      | 0        | 0       | 0           | 2       | 0              | 0              | 0                | 2             |
| Identity\/DevUsers.cs                | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Identity\/EmailKey.cs                | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Identity\/GrantAudit.cs              | N\/A   | 0      | 0        | 0       | 0           | 3       | 0              | 0              | 0                | 3             |
| Identity\/Group.cs                   | N\/A   | 0      | 0        | 0       | 0           | 5       | 0              | 0              | 0                | 5             |
| Identity\/GroupMember.cs             | N\/A   | 0      | 0        | 0       | 0           | 2       | 0              | 0              | 0                | 2             |
| Identity\/GroupRole.cs               | N\/A   | 0      | 0        | 0       | 0           | 2       | 0              | 0              | 0                | 2             |
| Identity\/Module.cs                  | N\/A   | 0      | 0        | 0       | 0           | 5       | 0              | 0              | 0                | 5             |
| Identity\/PermissionAction.cs        | N\/A   | 0      | 0        | 0       | 0           | 4       | 0              | 0              | 0                | 4             |
| Identity\/PermissionDefinition.cs    | N\/A   | 0      | 0        | 0       | 0           | 3       | 0              | 0              | 0                | 3             |
| Identity\/Permissions.cs             | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Identity\/RefreshToken.cs            | N\/A   | 0      | 0        | 0       | 0           | 3       | 0              | 0              | 0                | 3             |
| Identity\/Resource.cs                | N\/A   | 0      | 0        | 0       | 0           | 4       | 0              | 0              | 0                | 4             |
| Identity\/Role.cs                    | N\/A   | 0      | 0        | 0       | 0           | 5       | 0              | 0              | 0                | 5             |
| Identity\/RoleGrant.cs               | N\/A   | 0      | 0        | 0       | 0           | 2       | 0              | 0              | 0                | 2             |
| Identity\/Tenant.cs                  | N\/A   | 0      | 0        | 0       | 0           | 5       | 0              | 0              | 0                | 5             |
| Identity\/User.cs                    | N\/A   | 0      | 0        | 0       | 0           | 7       | 0              | 0              | 0                | 7             |
| Identity\/UserPermissionOverride.cs  | N\/A   | 0      | 0        | 0       | 0           | 2       | 0              | 0              | 0                | 2             |
| Identity\/UserRole.cs                | N\/A   | 0      | 0        | 0       | 0           | 2       | 0              | 0              | 0                | 2             |
| Ids\/GuidIdJsonConverter.cs          | N\/A   | 0      | 0        | 0       | 0           | 6       | 3              | 0              | 0                | 9             |
| Ids\/GuidIdTypeConverter.cs          | N\/A   | 0      | 0        | 0       | 0           | 10      | 1              | 0              | 0                | 11            |
| Ids\/Ids.cs                          | N\/A   | 0      | 0        | 0       | 0           | 184     | 46             | 0              | 0                | 230           |
| Ids\/IStronglyTypedId.cs             | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Ids\/Uuid7.cs                        | N\/A   | 0      | 0        | 0       | 0           | 43      | 8              | 0              | 0                | 51            |
| Results\/ErrorType.cs                | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Results\/FugueError.cs               | N\/A   | 0      | 0        | 0       | 0           | 2       | 0              | 0              | 0                | 2             |
| Results\/FugueErrors.cs              | N\/A   | 0      | 0        | 0       | 0           | 2       | 0              | 0              | 0                | 2             |
| Results\/IVersioned.cs               | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Results\/PagedResult.cs              | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Webhooks\/DataProtectionKeyRecord.cs | N\/A   | 0      | 0        | 0       | 0           | 4       | 0              | 0              | 0                | 4             |
| Webhooks\/EventTypeFilter.cs         | N\/A   | 0      | 0        | 0       | 0           | 10      | 0              | 0              | 0                | 10            |
| Webhooks\/OutboxMessage.cs           | N\/A   | 0      | 0        | 0       | 0           | 5       | 0              | 0              | 0                | 5             |
| Webhooks\/WebhookDelivery.cs         | N\/A   | 0      | 0        | 0       | 0           | 0       | 0              | 0              | 0                | 0             |
| Webhooks\/WebhookSubscription.cs     | N\/A   | 0      | 0        | 0       | 0           | 6       | 0              | 0              | 0                | 6             |

## The final mutation score is 94.37%

### *Coverage Thresholds: high:90 low:70 break:0*
