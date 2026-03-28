using Microsoft.AspNetCore.SignalR;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace Backend.Hubs;

public class ChatHub : Hub
{
    private readonly AppDbContext _context;

    public ChatHub(AppDbContext context)
    {
        _context = context;
    }

    public async Task SendMessage(int? chatId, int? groupId, int senderId, string message, string messageType = "text")
    {
        try
        {
            Console.WriteLine($"📨 SendMessage: chatId={chatId}, groupId={groupId}, senderId={senderId}, message={message}, messageType={messageType}");
            
            // Проверяем существование отправителя
            var sender = await _context.Users.FindAsync(senderId);
            if (sender == null)
            {
                Console.WriteLine($"❌ Отправитель с ID {senderId} не найден");
                return;
            }

            if (groupId.HasValue)
            {
                // Проверяем существование группы
                var group = await _context.Groups.FindAsync(groupId.Value);
                if (group == null)
                {
                    Console.WriteLine($"❌ Группа с ID {groupId} не найдена");
                    return;
                }

                // Групповое сообщение
                var newMessage = new Message
                {
                    GroupId = groupId.Value,
                    SenderId = senderId,
                    Content = message,
                    SentAt = DateTime.Now,
                    IsRead = false,
                    MessageType = messageType
                };

                Console.WriteLine($"📝 Добавляем сообщение в контекст...");
                _context.Messages.Add(newMessage);
                
                Console.WriteLine($"💾 Сохраняем в БД...");
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Групповое сообщение сохранено в БД, id={newMessage.Id}");

                Console.WriteLine($"📤 Отправляем через SignalR в group_{groupId}...");
                await Clients.Group($"group_{groupId}").SendAsync("ReceiveMessage", new
                {
                    newMessage.Id,
                    GroupId = newMessage.GroupId,
                    SenderId = newMessage.SenderId,
                    Content = newMessage.Content,
                    SentAt = newMessage.SentAt,
                    MessageType = newMessage.MessageType,
                    IsRead = newMessage.IsRead
                });
                Console.WriteLine($"✅ Сообщение отправлено в группу");
            }
            else if (chatId.HasValue)
            {
                // Проверяем существование чата
                var chat = await _context.Chats.FindAsync(chatId.Value);
                if (chat == null)
                {
                    Console.WriteLine($"❌ Чат с ID {chatId} не найден");
                    return;
                }

                // Личное сообщение
                var newMessage = new Message
                {
                    ChatId = chatId.Value,
                    SenderId = senderId,
                    Content = message,
                    SentAt = DateTime.Now,
                    IsRead = false,
                    MessageType = messageType
                };

                Console.WriteLine($"📝 Добавляем сообщение в контекст...");
                _context.Messages.Add(newMessage);
                
                Console.WriteLine($"💾 Сохраняем в БД...");
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Личное сообщение сохранено в БД, id={newMessage.Id}");

                Console.WriteLine($"📤 Отправляем через SignalR в chat_{chatId}...");
                await Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", new
                {
                    newMessage.Id,
                    ChatId = newMessage.ChatId,
                    SenderId = newMessage.SenderId,
                    Content = newMessage.Content,
                    SentAt = newMessage.SentAt,
                    MessageType = newMessage.MessageType,
                    IsRead = newMessage.IsRead
                });
                Console.WriteLine($"✅ Сообщение отправлено в чат");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка сохранения сообщения: {ex.Message}");
            Console.WriteLine($"❌ Тип ошибки: {ex.GetType().Name}");
            Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"❌ Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    public async Task JoinChat(int chatId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatId}");
        Console.WriteLine($"👤 Пользователь присоединился к чату {chatId}");
    }

    public async Task LeaveChat(int chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{chatId}");
        Console.WriteLine($"👤 Пользователь покинул чат {chatId}");
    }

    public async Task JoinGroup(int groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
        Console.WriteLine($"👤 Пользователь присоединился к группе {groupId}");
    }

    public async Task LeaveGroup(int groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
        Console.WriteLine($"👤 Пользователь покинул группу {groupId}");
    }
}