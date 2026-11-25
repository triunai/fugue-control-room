namespace Fugue.Application.Features.Groups;

/// <summary>A group with the roles its members inherit and how many members it has.</summary>
public sealed record GroupDto(string Code, string Name, bool IsActive, IReadOnlyList<string> Roles, int MemberCount);
