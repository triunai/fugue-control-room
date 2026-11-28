# Lane nth-editfix status (Refs D-064)

## Commits
- fix(api): edit allows no direct role when the user keeps access through an active group (slice 1/2)

## Behaviour
- `UpdateUserValidator` no longer rejects an empty role list; `UpdateUserHandler` returns 400 (`UserErrors.NoAccess`) only when
  the list is empty AND the user has no active group. Create is unchanged.
- `UserDirectory` now lists only ACTIVE groups in `UserDto.Groups` (matches what /me grants).

## Next
- slice 2/2: drawer validation + hint, spec, Bruno request.
