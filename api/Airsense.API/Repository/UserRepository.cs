using System.Data;
using Airsense.API.Models.Entity;
using Dapper;

namespace Airsense.API.Repository;

public class UserRepository(IDbConnection connection) : IUserRepository
{
    public async Task<bool> IsExistsByUidAsync(string uid)
    {
        const string sql = "SELECT 1 FROM users WHERE uid = @uid";
        var result = await connection.QueryAsync(sql, new { uid });
        return result.SingleOrDefault() != null;
    }

    public async Task<bool> IsExistsByIdAsync(int id)
    {
        const string sql = "SELECT 1 FROM users WHERE id = @id";
        var result = await connection.QueryAsync(sql, new { id });
        return result.SingleOrDefault() != null;
    }

    public async Task<User?> GetByUidAsync(string uid)
    {
        const string sql = """
                           SELECT 
                               id AS Id, 
                               uid AS Uid, 
                               name AS Name, 
                               email AS Email, 
                               notification_token AS NotificationToken,
                               created_at AS CreatedAt
                           FROM users
                           WHERE uid = @uid;
                           """;
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { uid });
    }

    public async Task<User> CreateAsync(User user)
    {
        const string sql = """
                           INSERT INTO users (uid, name, email)
                           VALUES (@Uid, @Name, @Email)
                           ON CONFLICT (uid) DO UPDATE SET
                               name = EXCLUDED.name,
                               email = EXCLUDED.email
                           RETURNING 
                               id AS Id, 
                               uid AS Uid, 
                               name AS Name, 
                               email AS Email, 
                               notification_token AS NotificationToken,
                               created_at AS CreatedAt;
                           """;
        var result = await connection.QuerySingleAsync<User>(sql, user);
        return result;
    }

    public async Task<User> CreateWithIdAsync(User user)
    {
        const string sql = """
                           INSERT INTO users (id, uid, name, email)
                           VALUES (@Id, @Uid, @Name, @Email)
                           ON CONFLICT (id) DO UPDATE SET
                               uid = EXCLUDED.uid,
                               name = EXCLUDED.name,
                               email = EXCLUDED.email
                           RETURNING 
                               id AS Id, 
                               uid AS Uid, 
                               name AS Name, 
                               email AS Email, 
                               notification_token AS NotificationToken,
                               created_at AS CreatedAt;
                           """;
        var result = await connection.QuerySingleAsync<User>(sql, user);

        const string resetSequenceSql = """
                                        SELECT setval(
                                            pg_get_serial_sequence('users', 'id'),
                                            (SELECT MAX(id) FROM users),
                                            true
                                        );
                                        """;
        await connection.ExecuteAsync(resetSequenceSql);

        return result;
    }

    public async Task SetNotificationTokenAsync(string uid, string token)
    {
        const string sql = "UPDATE users SET notification_token = @token WHERE uid = @uid";
        await connection.ExecuteAsync(sql, new { uid, token });
    }
    
    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = """
                           SELECT 
                               id AS Id, 
                               uid AS Uid, 
                               name AS Name, 
                               email AS Email, 
                               notification_token AS NotificationToken,
                               created_at AS CreatedAt
                           FROM users
                           WHERE email = @email;
                           """;
        var result = await connection.QuerySingleOrDefaultAsync<User>(sql, new { email });
        return result;
    }
}
