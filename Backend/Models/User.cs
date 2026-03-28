using System;
using System.Collections.Generic;

namespace Backend.Models;

public class User
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? VerificationCode { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<UserChat> UserChats { get; set; } = new List<UserChat>();
    public ICollection<BlockedUser> BlockedUsers { get; set; } = new List<BlockedUser>();
}