namespace Splitracker.Persistence.Model;

class GroupMember
{
    public required string UserId { get; set; }
    public required GroupRole Role { get; set; }
}