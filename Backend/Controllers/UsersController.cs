using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    
    public UsersController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }
    
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new { 
            message = "API работает", 
            time = DateTime.Now,
            status = "ok"
        });
    }
    
    [HttpPost("{id}/register")]
    public async Task<IActionResult> CompleteRegistration(int id, [FromForm] string username, IFormFile? avatar)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { message = "Пользователь не найден" });
            
            user.Username = username;
            
            if (avatar != null && avatar.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "avatars");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);
                
                var fileExtension = Path.GetExtension(avatar.FileName);
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }
                
                user.AvatarUrl = $"/uploads/avatars/{fileName}";
            }
            
            await _context.SaveChangesAsync();
            
            return Ok(new 
            { 
                user.Id, 
                user.Username, 
                user.AvatarUrl,
                PhoneNumber = user.PhoneNumber
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
    
    [HttpPut("{id}/profile")]
    public async Task<IActionResult> UpdateProfile(int id, [FromForm] string? username, IFormFile? avatar)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { message = "Пользователь не найден" });
            
            if (!string.IsNullOrWhiteSpace(username))
                user.Username = username;
            
            if (avatar != null && avatar.Length > 0)
            {
                // Удаляем старый аватар если есть
                if (!string.IsNullOrEmpty(user.AvatarUrl))
                {
                    var oldFilePath = Path.Combine(_env.WebRootPath ?? "wwwroot", user.AvatarUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                        System.IO.File.Delete(oldFilePath);
                }
                
                var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "avatars");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);
                
                var fileExtension = Path.GetExtension(avatar.FileName);
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }
                
                user.AvatarUrl = $"/uploads/avatars/{fileName}";
            }
            
            await _context.SaveChangesAsync();
            
            return Ok(new 
            { 
                user.Id, 
                user.Username, 
                user.AvatarUrl,
                user.PhoneNumber
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
    
    [HttpGet("search")]
    public async Task<IActionResult> SearchUser([FromQuery] string phoneNumber)
    {
        try
        {
            Console.WriteLine($"Searching for: {phoneNumber}");
            
            if (string.IsNullOrEmpty(phoneNumber))
                return BadRequest(new { message = "Номер не указан" });
            
            var user = await _context.Users
                .Where(u => u.PhoneNumber == phoneNumber)
                .Select(u => new 
                { 
                    u.Id, 
                    u.Username, 
                    u.PhoneNumber, 
                    u.AvatarUrl 
                })
                .FirstOrDefaultAsync();
                
            if (user == null)
                return NotFound(new { message = "Пользователь не найден" });
                
            return Ok(user);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка поиска" });
        }
    }
    
    [HttpDelete("delete-all")]
    public async Task<IActionResult> DeleteAllUsers()
    {
        try
        {
            var users = await _context.Users.ToListAsync();
            int count = users.Count;
            
            _context.Users.RemoveRange(users);
            await _context.SaveChangesAsync();
            
            return Ok(new { 
                message = $"✅ Удалено {count} пользователей",
                count = count 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                message = $"❌ Ошибка: {ex.Message}" 
            });
        }
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _context.Users
                .Select(u => new 
                { 
                    u.Id, 
                    u.Username, 
                    u.PhoneNumber, 
                    u.AvatarUrl,
                    u.IsVerified 
                })
                .ToListAsync();
                
            return Ok(users);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500);
        }
    }
}