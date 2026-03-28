using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlockController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public BlockController(AppDbContext context)
    {
        _context = context;
    }
    
    // Заблокировать пользователя
    [HttpPost("block")]
    public async Task<IActionResult> BlockUser([FromBody] BlockRequest request)
    {
        try
        {
            // Проверяем существование пользователей
            var user = await _context.Users.FindAsync(request.UserId);
            var blockedUser = await _context.Users.FindAsync(request.BlockedUserId);
            
            if (user == null || blockedUser == null)
            {
                return NotFound(new { message = "Пользователь не найден" });
            }
            
            // Нельзя заблокировать самого себя
            if (request.UserId == request.BlockedUserId)
            {
                return BadRequest(new { message = "Нельзя заблокировать самого себя" });
            }
            
            // Проверяем, не заблокирован ли уже
            var existingBlock = await _context.BlockedUsers
                .FirstOrDefaultAsync(b => b.UserId == request.UserId && b.BlockedUserId == request.BlockedUserId);
                
            if (existingBlock != null)
            {
                return BadRequest(new { message = "Пользователь уже заблокирован" });
            }
            
            // Создаем блокировку
            var block = new BlockedUser
            {
                UserId = request.UserId,
                BlockedUserId = request.BlockedUserId,
                BlockedAt = DateTime.Now
            };
            
            _context.BlockedUsers.Add(block);
            
            // Удаляем чат между пользователями (если есть)
            var chat = await _context.UserChats
                .Where(uc => uc.UserId == request.UserId || uc.UserId == request.BlockedUserId)
                .GroupBy(uc => uc.ChatId)
                .Where(g => g.Count() == 2)
                .Select(g => g.Key)
                .FirstOrDefaultAsync();
                
            if (chat != 0)
            {
                var messages = await _context.Messages.Where(m => m.ChatId == chat).ToListAsync();
                _context.Messages.RemoveRange(messages);
                
                var userChats = await _context.UserChats.Where(uc => uc.ChatId == chat).ToListAsync();
                _context.UserChats.RemoveRange(userChats);
                
                var chatEntity = await _context.Chats.FindAsync(chat);
                if (chatEntity != null)
                {
                    _context.Chats.Remove(chatEntity);
                }
            }
            
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Пользователь заблокирован" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка блокировки: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
    
    // Разблокировать пользователя
    [HttpPost("unblock")]
    public async Task<IActionResult> UnblockUser([FromBody] BlockRequest request)
    {
        try
        {
            var block = await _context.BlockedUsers
                .FirstOrDefaultAsync(b => b.UserId == request.UserId && b.BlockedUserId == request.BlockedUserId);
                
            if (block == null)
            {
                return NotFound(new { message = "Блокировка не найдена" });
            }
            
            _context.BlockedUsers.Remove(block);
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Пользователь разблокирован" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка разблокировки: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
    
    // Проверить, заблокирован ли пользователь
    [HttpGet("check")]
    public async Task<IActionResult> CheckBlocked([FromQuery] int userId, [FromQuery] int blockedUserId)
    {
        try
        {
            var isBlocked = await _context.BlockedUsers
                .AnyAsync(b => b.UserId == userId && b.BlockedUserId == blockedUserId);
                
            return Ok(new { isBlocked });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка проверки: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
    
    // Получить список заблокированных пользователей
    [HttpGet("list/{userId}")]
    public async Task<IActionResult> GetBlockedUsers(int userId)
    {
        try
        {
            var blockedUsers = await _context.BlockedUsers
                .Where(b => b.UserId == userId)
                .Include(b => b.BlockedUserInfo)
                .Select(b => new
                {
                    b.BlockedUserId,
                    Username = b.BlockedUserInfo != null ? b.BlockedUserInfo.Username : "Пользователь",
                    b.BlockedAt
                })
                .ToListAsync();
                
            return Ok(blockedUsers);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения списка: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }
}

public class BlockRequest
{
    public int UserId { get; set; }
    public int BlockedUserId { get; set; }
}