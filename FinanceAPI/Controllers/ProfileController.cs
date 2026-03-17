using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Profile;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : AuthenticatedControllerBase
{
    private readonly IUserService _userService;

    public ProfileController(IUserService userService)
    {
        _userService = userService;
    }

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        UserDto dto = await _userService.GetByIdAsync(UserId, cancellationToken);
        return Ok(dto);
    }

    // PUT /api/profile
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        UserDto dto = await _userService.UpdateProfileAsync(UserId, request.Username, request.Email, cancellationToken);
        return Ok(dto);
    }

    // PUT /api/profile/password
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.ChangePasswordAsync(UserId, request.CurrentPassword, request.NewPassword, cancellationToken);
        return NoContent();
    }

    // DELETE /api/profile
    [HttpDelete]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(UserId, cancellationToken);
        return NoContent();
    }

    // GET /api/profile/apikeys
    [HttpGet("apikeys")]
    public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetApiKeys(CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetApiKeysAsync(UserId, cancellationToken));
    }

    // POST /api/profile/apikeys
    [HttpPost("apikeys")]
    public async Task<ActionResult<ApiKeyCreatedResponse>> CreateApiKey([FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        ApiKeyCreatedResponse result = await _userService.CreateApiKeyAsync(UserId, request.Name, cancellationToken: cancellationToken);
        return CreatedAtAction(nameof(GetApiKeys), result);
    }

    // DELETE /api/profile/apikeys/{keyId}
    [HttpDelete("apikeys/{keyId:int}")]
    public async Task<IActionResult> RevokeApiKey(int keyId, CancellationToken cancellationToken)
    {
        await _userService.RevokeApiKeyAsync(UserId, keyId, cancellationToken);
        return NoContent();
    }
}
