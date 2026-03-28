using System;
using System.Collections.Generic;

namespace Backend.Models;

public class Chat
{
    public int Id { get; set; }
    public bool IsGroup { get; set; }
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<UserChat> UserChats { get; set; } = new List<UserChat>();
}

public class UserChat
{
    public int UserId { get; set; }
    public int ChatId { get; set; }
    
    public User User { get; set; } = null!;
    public Chat Chat { get; set; } = null!;
}