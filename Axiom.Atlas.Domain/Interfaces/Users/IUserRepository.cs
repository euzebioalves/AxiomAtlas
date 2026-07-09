using Axiom.Atlas.Domain.Entities.Users;

namespace Axiom.Atlas.Domain.Interfaces.Users
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<bool> CreateAsync(User user);
        Task UpdateAsync(User user);
    }
}
