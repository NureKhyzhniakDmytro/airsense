using System.Data;
using Dapper;

namespace Airsense.API.Repository;

public class ThresholdAlertStateRepository(IDbConnection connection) : IThresholdAlertStateRepository
{
    public async Task<ThresholdAlertTransition> UpdateAsync(
        int roomId,
        int sensorId,
        string parameter,
        double value,
        double criticalValue)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtext(@key)::bigint)",
                new { key = $"threshold-alert:{roomId}:{sensorId}:{parameter}" },
                transaction);

            var parameterId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT id FROM parameters WHERE name = @parameter",
                new { parameter },
                transaction);

            if (!parameterId.HasValue)
            {
                transaction.Commit();
                return ThresholdAlertTransition.None;
            }

            var isActive = value >= criticalValue;
            var previousIsActive = await connection.QuerySingleOrDefaultAsync<bool?>(
                """
                SELECT is_active
                FROM threshold_alert_states
                WHERE room_id = @roomId AND sensor_id = @sensorId AND parameter_id = @parameterId
                FOR UPDATE
                """,
                new { roomId, sensorId, parameterId },
                transaction);

            if (!previousIsActive.HasValue)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO threshold_alert_states(
                        room_id,
                        sensor_id,
                        parameter_id,
                        is_active,
                        last_value,
                        critical_value,
                        triggered_at,
                        resolved_at,
                        updated_at
                    )
                    VALUES (
                        @roomId,
                        @sensorId,
                        @parameterId,
                        @isActive,
                        @value,
                        @criticalValue,
                        CASE WHEN @isActive THEN CURRENT_TIMESTAMP ELSE NULL END,
                        CASE WHEN @isActive THEN NULL ELSE CURRENT_TIMESTAMP END,
                        CURRENT_TIMESTAMP
                    )
                    """,
                    new { roomId, sensorId, parameterId, isActive, value, criticalValue },
                    transaction);

                transaction.Commit();
                return isActive ? ThresholdAlertTransition.Triggered : ThresholdAlertTransition.None;
            }

            if (previousIsActive.Value == isActive)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE threshold_alert_states
                    SET last_value = @value,
                        critical_value = @criticalValue,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE room_id = @roomId AND parameter_id = @parameterId
                      AND sensor_id = @sensorId
                    """,
                    new { roomId, sensorId, parameterId, value, criticalValue },
                    transaction);

                transaction.Commit();
                return ThresholdAlertTransition.None;
            }

            await connection.ExecuteAsync(
                """
                UPDATE threshold_alert_states
                SET is_active = @isActive,
                    last_value = @value,
                    critical_value = @criticalValue,
                    triggered_at = CASE WHEN @isActive THEN CURRENT_TIMESTAMP ELSE triggered_at END,
                    resolved_at = CASE WHEN @isActive THEN resolved_at ELSE CURRENT_TIMESTAMP END,
                    updated_at = CURRENT_TIMESTAMP
                WHERE room_id = @roomId AND parameter_id = @parameterId
                  AND sensor_id = @sensorId
                """,
                new { roomId, sensorId, parameterId, isActive, value, criticalValue },
                transaction);

            transaction.Commit();
            return isActive ? ThresholdAlertTransition.Triggered : ThresholdAlertTransition.Resolved;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
