using System;

namespace Backend.Models;

public class BlockedUser
{
    public int Id { get; set; }
    public int UserId { get; set; } // Кто заблокировал
    public int BlockedUserId { get; set; } // Кого заблокировали
    public DateTime BlockedAt { get; set; }
    
    // Навигационные свойства - переименовал BlockedUser в BlockedUserInfo
    public User? User { get; set; }
    public User? BlockedUserInfo { get; set; } // Изменено с BlockedUser на BlockedUserInfo
}