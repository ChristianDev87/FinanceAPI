using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

public abstract class AuthenticatedControllerBase : ControllerBase
{
    protected int UserId
    {
        get
        {
            string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(value, out int userId))
                throw new UnauthorizedAccessException("Invalid user identifier in token.");
            return userId;
        }
    }
}
