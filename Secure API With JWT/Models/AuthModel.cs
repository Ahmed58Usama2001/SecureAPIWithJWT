namespace Secure_API_With_JWT.Models
{
    public class AuthModel
    {
        public string Message { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string Token { get; set; }
        public bool IsAuthinticated { get; set; }

        public DateTime ExpiresOn { get; set; }

        public List<string> Roles { get; set; }
    }
}
