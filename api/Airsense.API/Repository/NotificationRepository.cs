using System.Data;
using System.Text.Json;
using Airsense.API.Models.Dto.Notification;
using Dapper;

namespace Airsense.API.Repository;

public class NotificationRepository(IDbConnection connection) : INotificationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<ICollection<NotificationDeliveryDto>> AddAsync(
        ICollection<int> recipientUserIds,
        string title,
        string body,
        string severity,
        Dictionary<string, string>? data)
    {
        var recipients = recipientUserIds.Distinct().ToArray();
        if (recipients.Length == 0)
            return [];

        const string sql = """
                           INSERT INTO user_notifications(user_id, title, body, severity, data)
                           SELECT recipient_id, @title, @body, @severity, CAST(@dataJson AS jsonb)
                           FROM unnest(@recipients) AS recipient_id
                           RETURNING
                               user_id AS UserId,
                               id AS Id,
                               title AS Title,
                               body AS Body,
                               severity AS Severity,
                               data::text AS DataJson,
                               EXTRACT(EPOCH FROM created_at)::bigint AS CreatedAt,
                               EXTRACT(EPOCH FROM read_at)::bigint AS ReadAt
                           """;

        var rows = await connection.QueryAsync<NotificationRawDto>(sql, new
        {
            recipients,
            title,
            body,
            severity = string.IsNullOrWhiteSpace(severity) ? "info" : severity,
            dataJson = JsonSerializer.Serialize(data ?? new Dictionary<string, string>(), JsonOptions)
        });

        return rows.Select(row => new NotificationDeliveryDto
        {
            UserId = row.UserId,
            Notification = MapNotification(row)
        }).ToList();
    }

    public async Task<ICollection<NotificationDto>> GetAsync(int userId, int count, int skip)
    {
        const string sql = """
                           SELECT
                               id AS Id,
                               title AS Title,
                               body AS Body,
                               severity AS Severity,
                               data::text AS DataJson,
                               EXTRACT(EPOCH FROM created_at)::bigint AS CreatedAt,
                               EXTRACT(EPOCH FROM read_at)::bigint AS ReadAt
                           FROM user_notifications
                           WHERE user_id = @userId
                           ORDER BY created_at DESC, id DESC
                           LIMIT @count
                           OFFSET @skip
                           """;

        var rows = await connection.QueryAsync<NotificationRawDto>(sql, new { userId, count, skip });
        return rows.Select(MapNotification).ToList();
    }

    public async Task<int> CountAsync(int userId)
    {
        const string sql = "SELECT COUNT(*) FROM user_notifications WHERE user_id = @userId";
        return await connection.QuerySingleAsync<int>(sql, new { userId });
    }

    public async Task<int> CountUnreadAsync(int userId)
    {
        const string sql = "SELECT COUNT(*) FROM user_notifications WHERE user_id = @userId AND read_at IS NULL";
        return await connection.QuerySingleAsync<int>(sql, new { userId });
    }

    public async Task<bool> MarkReadAsync(int userId, long notificationId)
    {
        const string sql = """
                           UPDATE user_notifications
                           SET read_at = COALESCE(read_at, CURRENT_TIMESTAMP)
                           WHERE user_id = @userId AND id = @notificationId
                           """;
        return await connection.ExecuteAsync(sql, new { userId, notificationId }) > 0;
    }

    public async Task<int> MarkAllReadAsync(int userId)
    {
        const string sql = """
                           UPDATE user_notifications
                           SET read_at = CURRENT_TIMESTAMP
                           WHERE user_id = @userId AND read_at IS NULL
                           """;
        return await connection.ExecuteAsync(sql, new { userId });
    }

    private static NotificationDto MapNotification(NotificationRawDto row)
    {
        Dictionary<string, string>? data = null;
        if (!string.IsNullOrWhiteSpace(row.DataJson) && row.DataJson != "{}")
        {
            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, string>>(row.DataJson, JsonOptions);
            }
            catch (JsonException)
            {
                data = null;
            }
        }

        return new NotificationDto
        {
            Id = row.Id,
            Title = row.Title,
            Body = row.Body,
            Severity = row.Severity,
            Data = data,
            CreatedAt = row.CreatedAt,
            ReadAt = row.ReadAt
        };
    }
}
