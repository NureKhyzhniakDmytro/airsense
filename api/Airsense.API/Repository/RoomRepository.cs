using System.Data;
using System.Text.Json;
using Airsense.API.Models.Dto.Room;
using Airsense.API.Models.Dto.Sensor;
using Airsense.API.Models.Entity;
using Dapper;

namespace Airsense.API.Repository;

public class RoomRepository(IDbConnection connection) : IRoomRepository
{
    private static readonly JsonSerializerOptions LayoutJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static RoomLayoutDto CreateDefaultLayout() => new();

    public async Task<ICollection<RoomDto>> GetAsync(int envId, int skip, int count)
    {
        const string sql = """
                           WITH paged_rooms AS (
                               SELECT r.id, r.name, r.icon
                               FROM rooms r
                               WHERE r.environment_id = @envId
                               ORDER BY r.id
                               LIMIT @count
                               OFFSET @skip
                           ),
                           room_sensors AS (
                               SELECT s.id AS sensor_id, s.room_id
                               FROM sensors s
                               JOIN paged_rooms pr ON pr.id = s.room_id
                           ),
                           latest_sensor_data AS (
                               SELECT DISTINCT ON (sd.sensor_id, sd.parameter_id)
                                   rs.room_id,
                                   sd.sensor_id,
                                   sd.parameter_id,
                                   sd.value,
                                   sd.timestamp,
                                   p.name AS parameter,
                                   p.unit,
                                   p.min_value,
                                   p.max_value
                               FROM room_sensors rs
                               JOIN sensor_data sd ON sd.sensor_id = rs.sensor_id
                               JOIN parameters p ON sd.parameter_id = p.id
                               WHERE sd.timestamp > NOW() - INTERVAL '15 minutes'
                               ORDER BY sd.sensor_id, sd.parameter_id, sd.timestamp DESC
                           ),
                           room_devices AS (
                               SELECT d.id AS device_id, d.room_id
                               FROM devices d
                               JOIN paged_rooms pr ON pr.id = d.room_id
                           ),
                           latest_device_data AS (
                               SELECT DISTINCT ON (dd.device_id)
                                   rd.room_id,
                                   dd.device_id,
                                   dd.timestamp AS applied_at,
                                   dd.value AS DeviceSpeed
                               FROM room_devices rd
                               JOIN device_data dd ON dd.device_id = rd.device_id
                               WHERE dd.timestamp > NOW() - INTERVAL '15 minutes'
                               ORDER BY dd.device_id, dd.timestamp DESC, dd.value DESC
                           )
                           SELECT
                               pr.id AS Id,
                               pr.name AS Name,
                               pr.icon AS Icon,
                               MAX(ldd.DeviceSpeed) AS DeviceSpeed,
                               lsd.parameter AS ParamKey,
                               lsd.unit AS ParamUnit,
                               lsd.min_value AS ParamMinValue,
                               lsd.max_value AS ParamMaxValue,
                               AVG(lsd.value) AS ParamValue
                           FROM paged_rooms pr
                           LEFT JOIN latest_device_data ldd ON pr.id = ldd.room_id
                           LEFT JOIN latest_sensor_data lsd ON pr.id = lsd.room_id
                           GROUP BY pr.id, pr.name, pr.icon, lsd.parameter, lsd.unit, lsd.min_value, lsd.max_value
                           ORDER BY pr.id
                           """;

        var roomData = await connection.QueryAsync<RoomRawDto>(sql, new { envId, skip, count });

        var rooms = roomData
            .GroupBy(r => new { r.Id, r.Name, r.Icon })
            .Select(g => new RoomDto
            {
                Id = g.Key.Id,
                Name = g.Key.Name,
                Icon = g.Key.Icon,
                DeviceSpeed = g.Max(x => x.DeviceSpeed),
                Parameters = g
                    .Where(x => x.ParamKey is not null)
                    .Select(
                        x => new ParameterDto
                        {
                            Name = x.ParamKey,
                            Value = x.ParamValue.GetValueOrDefault(),
                            MinValue = x.ParamMinValue.GetValueOrDefault(),
                            MaxValue = x.ParamMaxValue.GetValueOrDefault(),
                            Unit = x.ParamUnit
                        }
                    ).ToList()
            })
            .Select(room =>
            {
                if (room.Parameters == null || room.Parameters.Count == 0)
                    room.Parameters = null;
                return room;
            });

        return rooms.ToList();
    }
    
    public async Task<int> CountAsync(int envId)
    {
        const string sql = "SELECT COUNT(*) FROM rooms r WHERE r.environment_id = @envId";
        var count = await connection.QuerySingleAsync<int>(sql, new { envId });
        return count;
    }
    
    public async Task<Room> CreateAsync(Room room)
    {
        const string sql = """
                           INSERT INTO rooms (name, environment_id, icon) 
                           VALUES (@Name, @EnvironmentId, @Icon) 
                           RETURNING 
                                 id AS Id,
                                 name AS Name,
                                 environment_id AS EnvironmentId,
                                 icon AS Icon
                           """;
        var result = await connection.QuerySingleAsync<Room>(sql, room);
        return result;
    }
    
    public async Task<RoomDto?> GetByIdAsync(int roomId)
    {
        const string sql = """
                           WITH room_scope AS (
                               SELECT r.id, r.name, r.icon
                               FROM rooms r
                               WHERE r.id = @roomId
                           ),
                           room_sensors AS (
                               SELECT s.id AS sensor_id, s.room_id
                               FROM sensors s
                               JOIN room_scope rs ON rs.id = s.room_id
                           ),
                           latest_sensor_data AS (
                               SELECT DISTINCT ON (sd.sensor_id, sd.parameter_id)
                                   rs.room_id,
                                   sd.sensor_id,
                                   sd.parameter_id,
                                   sd.value,
                                   sd.timestamp,
                                   p.name AS parameter,
                                   p.unit,
                                   p.min_value,
                                   p.max_value
                               FROM room_sensors rs
                               JOIN sensor_data sd ON sd.sensor_id = rs.sensor_id
                               JOIN parameters p ON sd.parameter_id = p.id
                               WHERE sd.timestamp > NOW() - INTERVAL '15 minutes'
                               ORDER BY sd.sensor_id, sd.parameter_id, sd.timestamp DESC
                           ),
                           room_devices AS (
                               SELECT d.id AS device_id, d.room_id
                               FROM devices d
                               JOIN room_scope rs ON rs.id = d.room_id
                           ),
                           latest_device_data AS (
                               SELECT DISTINCT ON (dd.device_id)
                                   rd.room_id,
                                   dd.device_id,
                                   dd.timestamp AS applied_at,
                                   dd.value AS DeviceSpeed
                               FROM room_devices rd
                               JOIN device_data dd ON dd.device_id = rd.device_id
                               WHERE dd.timestamp > NOW() - INTERVAL '15 minutes'
                               ORDER BY dd.device_id, dd.timestamp DESC, dd.value DESC
                           )
                           SELECT
                               rs.id AS Id,
                               rs.name AS Name,
                               rs.icon AS Icon,
                               MAX(ldd.DeviceSpeed) AS DeviceSpeed,
                               lsd.parameter AS ParamKey,
                               lsd.unit AS ParamUnit,
                               lsd.min_value AS ParamMinValue,
                               lsd.max_value AS ParamMaxValue,
                               AVG(lsd.value) AS ParamValue
                           FROM room_scope rs
                           LEFT JOIN latest_device_data ldd ON rs.id = ldd.room_id
                           LEFT JOIN latest_sensor_data lsd ON rs.id = lsd.room_id
                           GROUP BY rs.id, rs.name, rs.icon, lsd.parameter, lsd.unit, lsd.min_value, lsd.max_value
                           ORDER BY rs.id
                           """;
        var roomData = await connection.QueryAsync<RoomRawDto>(sql, new { roomId });

        var room = roomData
            .GroupBy(r => new { r.Id, r.Name, r.Icon })
            .Select(g => new RoomDto
            {
                Id = g.Key.Id,
                Name = g.Key.Name,
                Icon = g.Key.Icon,
                DeviceSpeed = g.Average(x => x.DeviceSpeed),
                Parameters = g
                    .Where(x => x.ParamKey is not null)
                    .Select(
                        x => new ParameterDto
                        {
                            Name = x.ParamKey,
                            Value = x.ParamValue.GetValueOrDefault(),
                            MinValue = x.ParamMinValue.GetValueOrDefault(),
                            MaxValue = x.ParamMaxValue.GetValueOrDefault(),
                            Unit = x.ParamUnit
                        }
                    ).ToList()
            })
            .Select(room =>
            {
                if (room.Parameters == null || room.Parameters.Count == 0)
                    room.Parameters = null;
                return room;
            });

        return room.FirstOrDefault();
    }
    
    public async Task UpdateAsync(int roomId, string name, string icon)
    {
        const string sql = "UPDATE rooms SET name = @name, icon = @icon WHERE id = @roomId";
        await connection.ExecuteAsync(sql, new { name, icon, roomId });
    }

    public async Task<RoomLayoutDto> GetLayoutAsync(int roomId)
    {
        const string sql = "SELECT layout::text FROM rooms WHERE id = @roomId";
        var layoutJson = await connection.QuerySingleOrDefaultAsync<string>(sql, new { roomId });
        if (string.IsNullOrWhiteSpace(layoutJson))
            return CreateDefaultLayout();

        try
        {
            return JsonSerializer.Deserialize<RoomLayoutDto>(layoutJson, LayoutJsonOptions) ?? CreateDefaultLayout();
        }
        catch (JsonException)
        {
            return CreateDefaultLayout();
        }
    }

    public async Task UpdateLayoutAsync(int roomId, RoomLayoutDto layout)
    {
        var layoutJson = JsonSerializer.Serialize(layout, LayoutJsonOptions);
        const string sql = "UPDATE rooms SET layout = CAST(@layoutJson AS jsonb) WHERE id = @roomId";
        await connection.ExecuteAsync(sql, new { roomId, layoutJson });
    }
    
    public async Task<bool> IsExistsAsync(int roomId, int envId)
    {
        const string sql = "SELECT 1 FROM rooms r WHERE r.id = @roomId AND r.environment_id = @envId";
        var result = await connection.QueryAsync(sql, new { roomId, envId });
        return result.SingleOrDefault() != null;
    }
    
    public async Task DeleteAsync(int roomId)
    {
        const string sql = "DELETE FROM rooms WHERE id = @roomId";
        await connection.ExecuteAsync(sql, new { roomId });
    }

    public async Task<bool> IsMemberAsync(int userId, int roomId)
    {
        const string sql = """
                           SELECT 1
                           FROM rooms r
                           JOIN environments e ON r.environment_id = e.id
                           JOIN environment_members em ON e.id = em.environment_id
                           WHERE r.id = @roomId AND em.member_id = @userId
                           """;
        var result = await connection.QueryAsync(sql, new { userId, roomId });
        return result.SingleOrDefault() != null;
    }
    
    public async Task<bool> IsHasAccessAsync(int userId, int roomId)
    {
        const string sql = """
                           SELECT 1 
                           FROM rooms r
                           JOIN environments e ON r.environment_id = e.id
                           JOIN environment_members em ON e.id = em.environment_id
                           WHERE r.id = @roomId AND em.member_id = @userId AND em.role <> 'user'  
                           """;
        var result = await connection.QueryAsync(sql, new { userId, roomId });
        return result.SingleOrDefault() != null;
    }
    
    public async Task<ICollection<ParameterDto>> GetAvailableTypesAsync(int roomId)
    {
        const string sql = """
                           SELECT DISTINCT ON (p.name)
                               p.name AS Name,
                               p.unit AS Unit,
                               p.min_value AS MinValue,   
                               p.max_value AS MaxValue
                           FROM sensors s
                           JOIN sensor_types t ON s.type_id = t.id
                           JOIN sensor_type_parameters tp ON t.id = tp.type_id
                           JOIN parameters p ON tp.parameter_id = p.id
                           WHERE s.room_id = @roomId
                           """;
        
        var types = await connection.QueryAsync<ParameterDto>(sql, new { roomId });
        return types.ToList();
    }
}
