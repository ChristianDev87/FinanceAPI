using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

public abstract class AuthenticatedControllerBase : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
