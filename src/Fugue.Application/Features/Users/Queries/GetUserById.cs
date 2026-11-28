using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Users.Models;
using Fugue.Application.Features.Users.Repos;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Users.Queries;

/// <summary>Contract.</summary>
public sealed record GetUserByIdQuery(UserId Id) : IQuery<Result<UserDto>>;

public sealed class GetUserByIdValidator : AbstractValidator<GetUserByIdQuery>
{
    public GetUserByIdValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class GetUserByIdHandler(IUserDirectory users) : IQueryHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken) =>
        await users.GetAsync(query.Id, cancellationToken) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<UserDto>(UserErrors.NotFound(query.Id));
}
