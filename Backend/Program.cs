using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Hubs;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настройка CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5041", "http://192.168.1.132:5041")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Настройка SignalR
builder.Services.AddSignalR();

// Настройка базы данных SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

// Регистрируем сервисы
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Применяем миграции при запуске
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    Console.WriteLine("✅ База данных проверена/создана");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ВАЖНО: добавляем поддержку статических файлов
app.UseStaticFiles();

app.UseCors("AllowFrontend");
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

// Создаем папки для загрузок, если их нет
var uploadsFolder = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads");
if (!Directory.Exists(uploadsFolder))
{
    Directory.CreateDirectory(uploadsFolder);
    Directory.CreateDirectory(Path.Combine(uploadsFolder, "avatars"));
    Directory.CreateDirectory(Path.Combine(uploadsFolder, "media"));
    Console.WriteLine("✅ Папки для загрузок созданы");
}
else
{
    Console.WriteLine("✅ Папки для загрузок уже существуют");
}

app.Run("http://0.0.0.0:5072");