using System.Security.Claims;
using Airsense.API.Models.Dto.Auth;
using Airsense.API.Models.Entity;
using Airsense.API.Repository;
using Airsense.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Airsense.API.Controllers;

[ApiController]
[Route("auth")]
[Authorize]
public class AuthController(
    IUserRepository userRepository,
    IAuthService authService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] AuthRequestDto request)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "You are not registered" });

        var name = User.FindFirstValue("name") ?? email;
        var user = await userRepository.GetByUidAsync(uid);
        var isCreated = false;
        if (user is null)
        {
            var existingEmailUser = await userRepository.GetByEmailAsync(email);
            if (existingEmailUser is not null)
            {
                existingEmailUser.Uid = uid;
                existingEmailUser.Name = name;
                existingEmailUser.Email = email;
                user = await userRepository.CreateWithIdAsync(existingEmailUser);
            }
            else
            {
                user = new User
                {
                    Name = name,
                    Email = email,
                    Uid = uid
                };
                user = await userRepository.CreateAsync(user);
                isCreated = true;
            }
        }

        var tokenId = User.FindFirstValue("id");
        if (tokenId != user.Id.ToString())
        {
            var result = await authService.SetIdAsync(uid, user.Id);
            if (!result)
                return StatusCode(500, new { Message = "Failed to set id" });
        }

        if (request.NotificationToken is not null)
            await userRepository.SetNotificationTokenAsync(uid, request.NotificationToken);

        return isCreated ? StatusCode(201) : NoContent();
    }
}
