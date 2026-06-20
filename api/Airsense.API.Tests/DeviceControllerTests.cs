using Airsense.API.Controllers;
using Airsense.API.Models.Dto.Auth;
using Airsense.API.Models.Dto.Device;
using Airsense.API.Models.Dto.Room;
using Airsense.API.Models.Entity;
using Airsense.API.Repository;
using Airsense.API.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Airsense.API.Tests;

public class DeviceControllerTests
{
    [Theory]
    [InlineData(null, "d-client")]
    [InlineData("", "d-client")]
    [InlineData("Bearer token", "d-client")]
    [InlineData("Basic not-base64", "d-client")]
    [InlineData("Basic bm9jb2xvbg==", "d-client")]
    [InlineData("Basic dXNlcjo=", "d-client")]
    [InlineData("Basic dXNlcjpwYXNz", null)]
    public async Task GetRoomId_ReturnsUnauthorizedForMalformedCredentials(string? authorization, string? clientId)
    {
        var controller = new DeviceController(new AllowingAuthService(), new FakeDeviceRepository());

        var result = await controller.GetRoomId(authorization, clientId);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetRoomId_ReturnsAssignedRoomForValidCredentials()
    {
        var controller = new DeviceController(new AllowingAuthService(), new FakeDeviceRepository());

        var result = await controller.GetRoomId("Basic ZGV2aWNlLTE6c2VjcmV0", "d-client");

        var ok = Assert.IsType<OkObjectResult>(result);
        var roomId = ok.Value?.GetType().GetProperty("RoomId")?.GetValue(ok.Value);
        Assert.Equal(42, roomId);
    }

    private sealed class AllowingAuthService : IAuthMqttService
    {
        public Task<string> AuthenticateAsync(MqttAuthRequestDto request) => Task.FromResult("allow");

        public Task<string> AuthorizeAsync(MqttAclRequestDto request) => Task.FromResult("allow");
    }

    private sealed class FakeDeviceRepository : IDeviceRepository
    {
        public Task<ICollection<DeviceDto>> GetAsync(int roomId, int count, int skip) => throw new NotImplementedException();

        public Task<Device?> GetByIdAsync(int deviceId) => throw new NotImplementedException();

        public Task<Device?> GetBySerialNumberAsync(string serialNumber) => Task.FromResult<Device?>(new Device
        {
            Id = 7,
            SerialNumber = serialNumber,
            RoomId = 42,
            Secret = "secret"
        });

        public Task<int> CountAsync(int roomId) => throw new NotImplementedException();

        public Task UpdateRoomAsync(int roomId, int deviceId) => throw new NotImplementedException();

        public Task DeleteRoomAsync(int deviceId) => throw new NotImplementedException();

        public Task AddDataAsync(int roomId, double speed) => throw new NotImplementedException();

        public Task<double?> GetFanSpeedAsync(string serialNumber) => throw new NotImplementedException();

        public Task<ICollection<HistoryDeviceDto>> GetRoomHistoryAsync(
            int roomId,
            DateTime fromDate,
            DateTime toDate,
            HistoryDto.HistoryInterval interval) => throw new NotImplementedException();

        public Task<HistoryDeviceDto?> GetDeviceHistoryAsync(
            int deviceId,
            DateTime fromDate,
            DateTime toDate,
            HistoryDto.HistoryInterval interval) => throw new NotImplementedException();
    }
}
