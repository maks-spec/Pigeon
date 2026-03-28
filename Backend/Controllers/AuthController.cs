using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public AuthController(AppDbContext context)
    {
        _context = context;
    }
    
    // Хеширование пароля
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Базовая валидация
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new { message = "Номер телефона обязателен" });
            }
            
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new { message = "Имя пользователя обязательно" });
            }
            
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Пароль обязателен" });
            }
            
            // Проверяем, существует ли пользователь
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
                
            if (existingUser != null)
            {
                return BadRequest(new { message = "Пользователь с таким номером уже существует" });
            }
            
            // Создаем нового пользователя
            var user = new User
            {
                PhoneNumber = request.PhoneNumber,
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                IsVerified = true,
                CreatedAt = DateTime.Now
            };
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            return Ok(new 
            { 
                user.Id, 
                user.Username, 
                user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                message = "Регистрация успешна"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка регистрации: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new { message = "Номер телефона обязателен" });
            }
            
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Пароль обязателен" });
            }
            
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
                
            if (user == null)
            {
                return BadRequest(new { message = "Пользователь не найден" });
            }
            
            // Проверяем пароль
            var passwordHash = HashPassword(request.Password);
            if (user.PasswordHash != passwordHash)
            {
                return BadRequest(new { message = "Неверный пароль" });
            }
            
            return Ok(new 
            { 
                user.Id, 
                user.Username, 
                user.PhoneNumber,
                user.AvatarUrl
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка входа: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
    
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { message = "Неверный формат запроса" });
            }
            
            // Находим пользователя
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId);
                
            if (user == null)
            {
                return BadRequest(new { message = "Пользователь не найден" });
            }
            
            // Проверяем текущий пароль
            var currentPasswordHash = HashPassword(request.CurrentPassword);
            if (user.PasswordHash != currentPasswordHash)
            {
                return BadRequest(new { message = "Неверный текущий пароль" });
            }
            
            // Проверяем, что новый пароль не пустой
            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Новый пароль не может быть пустым" });
            }
            
            // Устанавливаем новый пароль
            user.PasswordHash = HashPassword(request.NewPassword);
            
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Пароль успешно изменен" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка смены пароля: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
}

public class RegisterRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public int UserId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}