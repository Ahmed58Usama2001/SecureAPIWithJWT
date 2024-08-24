using Secure_API_With_JWT.Models;

namespace Secure_API_With_JWT.Services
{
    public interface IAuthService
    {
        Task<AuthModel>RegisterAsync(RegisterModel model);
        Task<AuthModel> GetTokenAsync(TokenRequestModel model);
        Task<string> AddRoleAsync(AddRoleModel model);
    }
}
