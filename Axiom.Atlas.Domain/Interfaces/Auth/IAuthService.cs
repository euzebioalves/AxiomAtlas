using Axiom.Atlas.Domain.Entities.Auth;

namespace Axiom.Atlas.Domain.Interfaces.Auth
{
    public interface IAuthService
    {
        Task<LoginResponse?> AuthenticateAsync(string username, string password);
    }
}
