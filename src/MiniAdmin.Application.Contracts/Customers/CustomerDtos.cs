namespace MiniAdmin.Application.Contracts.Customers;

public sealed record CustomerDto(
    string Id,
    string Title,
    string Type,
    string Content,
    int IsPublished,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt);

public sealed record CustomerListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null);

public sealed record SaveCustomerRequest(
    string Title,
    string Type,
    string Content,
    int IsPublished,
    DateTimeOffset? PublishedAt);
