using System.Security.Claims;
using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        return Ok(await _userService.GetAllAsync());
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<UserDto>> GetById(int userId)
    {
        return Ok(await _userService.GetByIdAsync(userId));
    }

    [HttpPut("{userId:int}")]
    public async Task<ActionResult<UserDto>> Update(int userId, [FromBody] UpdateUserRequest request)
    {
        return Ok(await _userService.UpdateAsync(userId, request));
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Delete(int userId)
    {
        await _userService.DeleteAsync(userId);
        return NoContent();
    }

    [HttpPost("{userId:int}/apikeys")]
    public async Task<ActionResult<ApiKeyCreatedResponse>> CreateApiKey(int userId, [FromBody] CreateApiKeyRequest request)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _userService.CreateApiKeyAsync(userId, request.Name, adminId);
        return Ok(result);
    }

    [HttpGet("{userId:int}/apikeys")]
    public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetApiKeys(int userId)
    {
        return Ok(await _userService.GetApiKeysAsync(userId));
    }

    [HttpDelete("{userId:int}/apikeys/{keyId:int}")]
    public async Task<IActionResult> RevokeApiKey(int userId, int keyId)
    {
        await _userService.RevokeApiKeyAsync(userId, keyId);
        return NoContent();
    }
}
