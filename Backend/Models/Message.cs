using System;

namespace Backend.Models;

public class Message
{
    public int Id { get; set; }
    public int? ChatId { get; set; }
    public int? GroupId { get; set; }  // Важно: должно быть int?, а не int
    public int SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public string MessageType { get; set; } = "text"; // text, image, video
    
    public Chat? Chat { get; set; }
    public Group? Group { get; set; }
    public User Sender { get; set; } = null!;
}