namespace VoucherTracker.Api.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Sender"; // "Sender" or "Admin"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}