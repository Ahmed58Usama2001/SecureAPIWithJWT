using Microsoft.EntityFrameworkCore;

namespace Secure_API_With_JWT.Models;

[Owned]
public class RefreshToken
{
    public string Token { get; set; }
    public DateTime? ExpiresOn { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;

    public DateTime CreatedOn { get; set; }

    public DateTime? RevokedOn { get; set; }

    public bool IsActive => !IsExpired && RevokedOn == null;

}
