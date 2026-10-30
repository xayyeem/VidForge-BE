using Microsoft.EntityFrameworkCore;
using VideoUploadSystem.Application.Interfaces;
using VideoUploadSystem.Infrastructure.Data;
using VideoUploadSystem.Infrastructure.Messaging;
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