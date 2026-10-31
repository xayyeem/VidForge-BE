using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoUploadSystem.Application.Interfaces;
using VideoUploadSystem.Infrastructure.Configuration;
using VideoUploadSystem.Infrastructure.Data;
using VideoUploadSystem.Infrastructure.Messaging;
using VideoUploadSystem.Infrastructure.Services;
using VideoUploadSystem.Infrastructure.Storage;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod().AllowCredentials(); ;
    });
});
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<RabbitMqPublisher>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

// Register Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    );
});
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection("Gemini"));

builder.Services.AddHttpClient<
    IThumbnailGenerationService,
    GeminiThumbnailGenerationService>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<GeminiOptions>>()
            .Value;

        var apiKey = builder.Configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromMinutes(3);
        client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
    });
builder.Services.AddScoped<IVideoStorageService, LocalVideoStorageService>();

var app = builder.Build();
app.UseCors("ReactApp");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();