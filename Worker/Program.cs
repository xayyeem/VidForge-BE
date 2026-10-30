using Microsoft.EntityFrameworkCore;
using VideoUploadSystem.Application.Interfaces;
using VideoUploadSystem.Infrastructure.Data;
using VideoUploadSystem.Infrastructure.Media;
using VideoUploadSystem.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
// FFmpeg configuration
builder.Services.Configure<FfmpegOptions>(
    builder.Configuration.GetSection("Ffmpeg"));

// IMPORTANT: Register video processing service
builder.Services.AddScoped<IVideoProcessingService, FfmpegVideoProcessingService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();