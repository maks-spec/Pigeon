using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.SignalR;
using Backend.Hubs;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatsController(AppDbContext context, IWebHostEnvironment env, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _env = env;
        _hubContext = hubContext;
        Console.WriteLine("✅ ChatsController инициализирован");
    }
    
    [HttpPost("create")]
    public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
    {
        try
        {
            Console.WriteLine($"📝 CreateChat: UserId1={request.UserId1}, UserId2={request.UserId2}");
            
            // Проверяем, не заблокировал ли один пользователь другого
            var isBlocked = await _context.BlockedUsers
                .AnyAsync(b => (b.UserId == request.UserId1 && b.BlockedUserId == request.UserId2) ||
                              (b.UserId == request.UserId2 && b.BlockedUserId == request.UserId1));
            
            if (isBlocked)
            {
                return BadRequest(new { message = "Невозможно создать чат - пользователь заблокирован" });
            }
            
            // Проверяем существование пользователей
            var user1 = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId1 && u.IsVerified);
            var user2 = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId2 && u.IsVerified);
            
            if (user1 == null)
            {
                Console.WriteLine($"❌ Пользователь {request.UserId1} не найден или не верифицирован");
                return NotFound(new { message = $"Пользователь {request.UserId1} не найден" });
            }
            
            if (user2 == null)
            {
                Console.WriteLine($"❌ Пользователь {request.UserId2} не найден или не верифицирован");
                return NotFound(new { message = $"Пользователь {request.UserId2} не найден" });
            }
            
            // Проверяем, есть ли уже чат между этими пользователями
            var existingChat = await _context.UserChats
                .Where(uc => uc.UserId == request.UserId1 || uc.UserId == request.UserId2)
                .GroupBy(uc => uc.ChatId)
                .Where(g => g.Count() == 2)
                .Select(g => g.Key)
                .FirstOrDefaultAsync();
                
            if (existingChat != 0)
            {
                Console.WriteLine($"✅ Найден существующий чат: {existingChat}");
                return Ok(new { chatId = existingChat, isNew = false });
            }
            
            // Создаем новый чат
            var chat = new Chat
            {
                IsGroup = false,
                CreatedAt = DateTime.Now
            };
            
            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ Создан новый чат: {chat.Id}");
            
            // Добавляем пользователей в чат
            _context.UserChats.Add(new UserChat { UserId = request.UserId1, ChatId = chat.Id });
            _context.UserChats.Add(new UserChat { UserId = request.UserId2, ChatId = chat.Id });
            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ Пользователи добавлены в чат");
            
            return Ok(new { chatId = chat.Id, isNew = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка создания чата" });
        }
    }
    
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserChats(int userId)
    {
        try
        {
            Console.WriteLine($"📝 GetUserChats: userId={userId}");
            
            var chats = await _context.UserChats
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.Chat)
                    .ThenInclude(c => c.Messages)
                .Include(uc => uc.Chat)
                    .ThenInclude(c => c.UserChats)
                        .ThenInclude(uc => uc.User)
                .Select(uc => new
                {
                    ChatId = uc.ChatId,
                    OtherUser = uc.Chat.UserChats
                        .Where(x => x.UserId != userId)
                        .Select(x => new
                        {
                            x.User.Id,
                            x.User.Username,
                            AvatarUrl = x.User.AvatarUrl
                        }).FirstOrDefault(),
                    LastMessage = uc.Chat.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => new { 
                            m.Content, 
                            m.SentAt, 
                            m.SenderId, 
                            m.MessageType,
                            Preview = m.MessageType == "image" ? "📷 Фото" : 
                                     m.MessageType == "video" ? "🎥 Видео" : 
                                     m.Content.Length > 30 ? m.Content.Substring(0, 30) + "..." : m.Content
                        })
                        .FirstOrDefault(),
                    UnreadCount = uc.Chat.Messages
                        .Count(m => !m.IsRead && m.SenderId != userId)
                })
                .ToListAsync();
                
            Console.WriteLine($"✅ Найдено чатов: {chats.Count}");
            return Ok(chats);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка загрузки чатов" });
        }
    }
    
    [HttpGet("{chatId}/messages")]
    public async Task<IActionResult> GetMessages(int chatId)
    {
        try
        {
            Console.WriteLine($"📝 GetMessages: chatId={chatId}");
            
            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.SentAt,
                    m.SenderId,
                    m.IsRead,
                    m.MessageType
                })
                .ToListAsync();
                
            Console.WriteLine($"✅ Найдено сообщений: {messages.Count}");
            return Ok(messages);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка загрузки сообщений" });
        }
    }

    [HttpGet("group/{groupId}/messages")]
    public async Task<IActionResult> GetGroupMessages(int groupId)
    {
        try
        {
            Console.WriteLine($"📝 GetGroupMessages: groupId={groupId}");
            
            var messages = await _context.Messages
                .Where(m => m.GroupId == groupId)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.SentAt,
                    m.SenderId,
                    m.IsRead,
                    m.MessageType
                })
                .ToListAsync();
                
            Console.WriteLine($"✅ Найдено сообщений группы: {messages.Count}");
            return Ok(messages);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка загрузки сообщений группы: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка загрузки сообщений группы" });
        }
    }
    
    [HttpPost("send-media")]
    public async Task<IActionResult> SendMedia([FromForm] int chatId, [FromForm] int senderId, IFormFile file)
    {
        try
        {
            Console.WriteLine($"📝 SendMedia: chatId={chatId}, senderId={senderId}, file={file?.FileName}");
            
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Файл не выбран" });
            
            if (file.Length > 50 * 1024 * 1024)
                return BadRequest(new { message = "Файл слишком большой (макс 50MB)" });
            
            var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "media");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            
            var fileExtension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            
            var fileType = file.ContentType.StartsWith("image") ? "image" : 
                          file.ContentType.StartsWith("video") ? "video" : "file";
            
            var fileUrl = $"/uploads/media/{fileName}";
            
            var message = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = fileUrl,
                SentAt = DateTime.Now,
                IsRead = false,
                MessageType = fileType
            };
            
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ Файл сохранен: {fileName}");
            
            // ОТПРАВЛЯЕМ СООБЩЕНИЕ ЧЕРЕЗ SIGNALR ВСЕМ УЧАСТНИКАМ ЧАТА
            await _hubContext.Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", new
            {
                message.Id,
                ChatId = message.ChatId,
                GroupId = (int?)null,
                message.SenderId,
                message.Content,
                message.SentAt,
                message.MessageType,
                message.IsRead
            });
            
            return Ok(new
            {
                message.Id,
                message.Content,
                message.SentAt,
                message.SenderId,
                ChatId = message.ChatId,
                GroupId = (int?)null,
                message.MessageType
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка при отправке файла" });
        }
    }

    [HttpPost("send-group-media")]
    public async Task<IActionResult> SendGroupMedia([FromForm] int groupId, [FromForm] int senderId, IFormFile file)
    {
        try
        {
            Console.WriteLine($"📝 SendGroupMedia: groupId={groupId}, senderId={senderId}, file={file?.FileName}");
            
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Файл не выбран" });
            
            if (file.Length > 50 * 1024 * 1024)
                return BadRequest(new { message = "Файл слишком большой (макс 50MB)" });
            
            var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "media");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            
            var fileExtension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            
            var fileType = file.ContentType.StartsWith("image") ? "image" : 
                          file.ContentType.StartsWith("video") ? "video" : "file";
            
            var fileUrl = $"/uploads/media/{fileName}";
            
            var message = new Message
            {
                GroupId = groupId,
                SenderId = senderId,
                Content = fileUrl,
                SentAt = DateTime.Now,
                IsRead = false,
                MessageType = fileType
            };
            
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ Файл сохранен в группе: {fileName}");
            
            // ОТПРАВЛЯЕМ СООБЩЕНИЕ ЧЕРЕЗ SIGNALR ВСЕМ УЧАСТНИКАМ ГРУППЫ
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("ReceiveMessage", new
            {
                message.Id,
                ChatId = (int?)null,
                GroupId = message.GroupId,
                message.SenderId,
                message.Content,
                message.SentAt,
                message.MessageType,
                message.IsRead
            });
            
            return Ok(new
            {
                message.Id,
                message.Content,
                message.SentAt,
                message.SenderId,
                ChatId = (int?)null,
                GroupId = message.GroupId,
                message.MessageType
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка при отправке файла в группу" });
        }
    }

    [HttpPost("send-group-message")]
    public async Task<IActionResult> SendGroupMessage([FromBody] SendGroupMessageRequest request)
    {
        try
        {
            var message = new Message
            {
                GroupId = request.GroupId,
                SenderId = request.SenderId,
                Content = request.Content,
                SentAt = DateTime.Now,
                IsRead = false,
                MessageType = request.MessageType ?? "text"
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Отправляем через SignalR
            await _hubContext.Clients.Group($"group_{request.GroupId}").SendAsync("ReceiveMessage", new
            {
                message.Id,
                GroupId = message.GroupId,
                message.SenderId,
                message.Content,
                message.SentAt,
                message.MessageType,
                message.IsRead
            });

            return Ok(new
            {
                message.Id,
                message.Content,
                message.SentAt,
                message.SenderId,
                message.GroupId,
                message.MessageType
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка отправки сообщения в группу: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка сервера" });
        }
    }

    public class SendGroupMessageRequest
    {
        public int GroupId { get; set; }
        public int SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? MessageType { get; set; }
    }
}

public class CreateChatRequest
{
    public int UserId1 { get; set; }
    public int UserId2 { get; set; }
}