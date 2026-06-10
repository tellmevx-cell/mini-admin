namespace MiniAdmin.Application.Contracts.Users;

public enum DeleteUserResult
{
    Deleted = 0,
    NotFound = 1,
    Forbidden = 2,
    BuiltInAdmin = 3,
    CurrentUser = 4,
    LastAdministrator = 5
}
