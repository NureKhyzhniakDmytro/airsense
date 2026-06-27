using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Airsense.API.Repository;
using Airsense.API.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MQTTnet;
using Npgsql;

var builder = WebApplication.CreateSlimBuilder(args);
var serviceRole = builder.Configuration["Airsense:ServiceRole"] ?? ServiceRoles.Api;
var isApiService = ServiceRoles.IsApi(serviceRole);
var firebaseCredentialsPath = builder.Configuration["Firebase:CredentialsFileLocation"];
var firebaseProjectName = builder.Configuration["Firebase:ProjectName"];
var isFirebaseConfigured = !string.IsNullOrWhiteSpace(firebaseCredentialsPath)
                           && File.Exists(firebaseCredentialsPath)
                           && !string.IsNullOrWhiteSpace(firebaseProjectName);

if (isApiService)
    builder.Services.AddControllers();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{firebaseProjectName}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{firebaseProjectName}",
            ValidateAudience = true,
            ValidAudience = firebaseProjectName,
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

if (isFirebaseConfigured)
{
    builder.Services.AddSingleton(FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromFile(firebaseCredentialsPath)
    }));
}

builder.Services.AddMvcCore()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    });

builder.Services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<ISensorRepository, SensorRepository>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IThresholdAlertStateRepository, ThresholdAlertStateRepository>();
builder.Services.AddHttpClient();

builder.Services.AddScoped<IAuthService>(_ => isFirebaseConfigured ? new FirebaseAuthService() : new NoOpAuthService());
builder.Services.AddScoped<IAuthMqttService, AuthMqttService>();
builder.Services.AddScoped<ISensorService, SensorService>();
builder.Services.AddScoped<IAiPredictionService, AiPredictionService>();
builder.Services.AddScoped<INotificationService>(sp => isFirebaseConfigured
    ? new FirebaseNotificationService(sp.GetRequiredService<ILogger<FirebaseNotificationService>>())
    : new NoOpNotificationService());
builder.Services.AddSingleton<IRoomLiveTelemetryHub, RoomLiveTelemetryHub>();
builder.Services.AddSingleton<IUserNotificationHub, UserNotificationHub>();

if (ServiceRoles.IsApi(serviceRole))
{
    builder.Services.AddSingleton<MqttCommandService>();
    builder.Services.AddSingleton<IMqttService>(sp => sp.GetRequiredService<MqttCommandService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttCommandService>());
}
else if (ServiceRoles.IsTelemetryIngestion(serviceRole))
{
    builder.Services.AddSingleton<TelemetryIngestionMqttService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TelemetryIngestionMqttService>());
}
else if (ServiceRoles.IsAutomation(serviceRole))
{
    builder.Services.AddSingleton<AutomationMqttService>();
    builder.Services.AddSingleton<IMqttService>(sp => sp.GetRequiredService<AutomationMqttService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AutomationMqttService>());
}
else if (ServiceRoles.IsNotification(serviceRole))
{
    builder.Services.AddSingleton<NotificationMqttService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<NotificationMqttService>());
}

builder.Services.AddSingleton(new MqttClientOptionsBuilder()
    .WithClientId($"api-{serviceRole}-{Environment.MachineName}")
    .WithConnectionUri(builder.Configuration["Mqtt:ConnectionUri"])
    .Build()
);

var app = builder.Build();

app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    role = serviceRole
}));

if (isApiService)
    app.MapControllers();

app.Run();
