using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public GroupsController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    // Создать группу
    [HttpPost("create")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
    {
        try
        {
            var creator = await _context.Users.FindAsync(request.CreatorId);
            if (creator == null)
                return NotFound(new { message = "Пользователь не найден" });

            // Генерируем уникальный 6-значный код
            string inviteCode;
            do
            {
                inviteCode = GenerateInviteCode();
            } while (await _context.Groups.AnyAsync(g => g.InviteCode == inviteCode));

            var group = new Group
            {
                Name = request.Name,
                InviteCode = inviteCode,
                CreatorId = request.CreatorId,
                CreatedAt = DateTime.Now
            };

            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            // Добавляем создателя как администратора
            var groupMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = request.CreatorId,
                IsAdmin = true,
                JoinedAt = DateTime.Now
            };

            _context.GroupMembers.Add(groupMember);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                group.Id,
                group.Name,
                group.InviteCode,
                group.CreatorId,
                group.CreatedAt,
                group.AvatarUrl
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка создания группы: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    // Присоединиться к группе по коду
    [HttpPost("join")]
    public async Task<IActionResult> JoinGroup([FromBody] JoinGroupRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
                return NotFound(new { message = "Пользователь не найден" });

            var group = await _context.Groups
                .FirstOrDefaultAsync(g => g.InviteCode == request.InviteCode);

            if (group == null)
                return NotFound(new { message = "Группа не найдена" });

            // Проверяем, не состоит ли уже пользователь в группе
            var existingMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == request.UserId);

            if (existingMember)
                return BadRequest(new { message = "Вы уже в этой группе" });

            var groupMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = request.UserId,
                IsAdmin = false,
                JoinedAt = DateTime.Now
            };

            _context.GroupMembers.Add(groupMember);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                group.Id,
                group.Name,
                group.InviteCode,
                group.AvatarUrl,
                IsAdmin = false
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка присоединения к группе: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    // Получить информацию о группе
    [HttpGet("{groupId}")]
    public async Task<IActionResult> GetGroup(int groupId)
    {
        try
        {
            var group = await _context.Groups
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                return NotFound(new { message = "Группа не найдена" });

            return Ok(new
            {
                group.Id,
                group.Name,
                group.InviteCode,
                group.AvatarUrl,
                group.CreatorId,
                group.CreatedAt,
                Members = group.Members.Select(m => new
                {
                    m.UserId,
                    Username = m.User?.Username,
                    AvatarUrl = m.User?.AvatarUrl,
                    m.IsAdmin,
                    m.JoinedAt
                })
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения группы: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    // Получить все группы пользователя
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserGroups(int userId)
    {
        try
        {
            var groups = await _context.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Include(gm => gm.Group)
                .Select(gm => new
                {
                    gm.Group.Id,
                    gm.Group.Name,
                    gm.Group.AvatarUrl,
                    gm.Group.InviteCode,
                    gm.Group.CreatorId,
                    gm.Group.CreatedAt,
                    gm.IsAdmin,
                    MemberCount = _context.GroupMembers.Count(x => x.GroupId == gm.GroupId),
                    LastMessage = _context.Messages
                        .Where(m => m.GroupId == gm.GroupId)
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => new
                        {
                            m.Content,
                            m.SentAt,
                            m.SenderId,
                            m.MessageType,
                            Preview = m.MessageType == "image" ? "📷 Фото" :
                                     m.MessageType == "video" ? "🎥 Видео" :
                                     m.Content.Length > 30 ? m.Content.Substring(0, 30) + "..." : m.Content
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(groups);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения групп: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    // Обновить информацию о группе (только для админов)
    [HttpPut("{groupId}")]
    public async Task<IActionResult> UpdateGroup(int groupId, [FromForm] string? name, IFormFile? avatar)
    {
        try
        {
            var group = await _context.Groups.FindAsync(groupId);
            if (group == null)
                return NotFound(new { message = "Группа не найдена" });

            if (!string.IsNullOrWhiteSpace(name))
                group.Name = name;

            if (avatar != null && avatar.Length > 0)
            {
                // Удаляем старый аватар если есть
                if (!string.IsNullOrEmpty(group.AvatarUrl))
                {
                    var oldFilePath = Path.Combine(_env.WebRootPath ?? "wwwroot", group.AvatarUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                        System.IO.File.Delete(oldFilePath);
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "group_avatars");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileExtension = Path.GetExtension(avatar.FileName);
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }

                group.AvatarUrl = $"/uploads/group_avatars/{fileName}";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                group.Id,
                group.Name,
                group.AvatarUrl,
                group.InviteCode
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обновления группы: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    // Удалить группу (только для создателя)
    [HttpDelete("{groupId}")]
    public async Task<IActionResult> DeleteGroup(int groupId, [FromQuery] int userId)
    {
        try
        {
            var group = await _context.Groups
                .Include(g => g.Members)
                .Include(g => g.Messages)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                return NotFound(new { message = "Группа не найдена" });

            // Проверяем, что пользователь является создателем
            if (group.CreatorId != userId)
                return Forbid();

            // Удаляем все сообщения группы
            if (group.Messages.Any())
                _context.Messages.RemoveRange(group.Messages);
            
            // Удаляем всех участников
            if (group.Members.Any())
                _context.GroupMembers.RemoveRange(group.Members);
            
            // Удаляем группу
            _context.Groups.Remove(group);
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Группа удалена" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка удаления группы: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    // Удалить участника из группы (только для админов)
    [HttpDelete("{groupId}/member/{userId}")]
    public async Task<IActionResult> RemoveMember(int groupId, int userId, [FromQuery] int adminId)
    {
        try
        {
            // Проверяем, что админ имеет права
            var admin = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == adminId && gm.IsAdmin);

            if (admin == null)
                return Forbid();

            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (member == null)
                return NotFound(new { message = "Участник не найден" });

            // Нельзя удалить создателя группы
            var group = await _context.Groups.FindAsync(groupId);
            if (group != null && group.CreatorId == userId)
                return BadRequest(new { message = "Нельзя удалить создателя группы" });

            _context.GroupMembers.Remove(member);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Участник удален" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка удаления участника: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    // Назначить администратора (только для создателя)
    [HttpPost("{groupId}/set-admin")]
    public async Task<IActionResult> SetAdmin(int groupId, [FromBody] SetAdminRequest request)
    {
        try
        {
            var group = await _context.Groups.FindAsync(groupId);
            if (group == null)
                return NotFound(new { message = "Группа не найдена" });

            // Только создатель может назначать админов
            if (group.CreatorId != request.AdminId)
                return Forbid();

            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == request.UserId);

            if (member == null)
                return NotFound(new { message = "Участник не найден" });

            member.IsAdmin = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Администратор назначен" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка назначения администратора: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    private string GenerateInviteCode()
    {
        Random random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}

public class CreateGroupRequest
{
    public int CreatorId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class JoinGroupRequest
{
    public int UserId { get; set; }
    public string InviteCode { get; set; } = string.Empty;
}

public class SetAdminRequest
{
    public int AdminId { get; set; }
    public int UserId { get; set; }
}