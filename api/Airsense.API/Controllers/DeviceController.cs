using System.Text;
using Airsense.API.Repository;
using Airsense.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Airsense.API.Controllers;

[ApiController]
[Route("device")]
public class DeviceController(
    IAuthMqttService authService,
    IDeviceRepository deviceRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRoomId(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromHeader(Name = "Client-Id")] string? clientId
    )
    {
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(clientId))
            return Unauthorized();

        string decodedString;
        try
        {
            var data = Convert.FromBase64String(authorization["Basic ".Length..]);
            decodedString = Encoding.UTF8.GetString(data);
        }
        catch (FormatException)
        {
            return Unauthorized();
        }

        var separatorIndex = decodedString.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == decodedString.Length - 1)
            return Unauthorized();

        var username = decodedString[..separatorIndex];
        var password = decodedString[(separatorIndex + 1)..];

        var result = await authService.AuthenticateAsync(new()
        {
            ClientId = clientId,
            Username = username,
            Password = password
        });

        if (!result.Equals("allow"))
            return Unauthorized();
        
        var device = await deviceRepository.GetBySerialNumberAsync(username);
        if (device is null)
            return BadRequest();
        
        return Ok(new { device.RoomId });
    }
}
