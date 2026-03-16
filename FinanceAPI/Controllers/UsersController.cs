using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : AuthenticatedControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<UserDto>> GetById(int userId, CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetByIdAsync(userId, cancellationToken));
    }

    [HttpPut("{userId:int}")]
    public async Task<ActionResult<UserDto>> Update(int userId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _userService.UpdateAsync(userId, request, allowRoleChange: true, cancellationToken: cancellationToken));
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Delete(int userId, CancellationToken cancellationToken)
    {
        if (userId == UserId)
        {
            throw new InvalidOperationException("You cannot delete your own account.");
        }

        await _userService.DeleteAsync(userId, cancellationToken);
        return NoContent();
    }

    // PUT /api/users/{userId}/active
    [HttpPut("{userId:int}/active")]
    public async Task<IActionResult> SetActive(int userId, [FromBody] bool isActive, CancellationToken cancellationToken)
    {
        if (userId == UserId)
        {
            throw new InvalidOperationException("You cannot deactivate your own account.");
        }

        await _userService.SetActiveAsync(userId, isActive, cancellationToken);
        return NoContent();
    }

    // PUT /api/users/{userId}/password
    [HttpPut("{userId:int}/password")]
    public async Task<IActionResult> SetPassword(int userId, [FromBody] AdminSetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.AdminSetPasswordAsync(userId, request.NewPassword, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:int}/apikeys")]
    public async Task<ActionResult<ApiKeyCreatedResponse>> CreateApiKey(int userId, [FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        ApiKeyCreatedResponse result = await _userService.CreateApiKeyAsync(userId, request.Name, createdByAdminId: UserId, cancellationToken: cancellationToken);
        return Ok(result);
    }

    [HttpGet("{userId:int}/apikeys")]
    public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetApiKeys(int userId, CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetApiKeysAsync(userId, cancellationToken));
    }

    [HttpDelete("{userId:int}/apikeys/{keyId:int}")]
    public async Task<IActionResult> RevokeApiKey(int userId, int keyId, CancellationToken cancellationToken)
    {
        await _userService.RevokeApiKeyAsync(userId, keyId, cancellationToken);
        return NoContent();
    }
}
