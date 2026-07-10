using Axiom.Atlas.Domain.Entities.Users;
using Axiom.Atlas.Domain.Interfaces.Users;
using Axiom.Atlas.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Axiom.Atlas.Infrastructure.Repositories.Users
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CreateAsync(User user)
        {
            try
            {
                await _context.Users.AddAsync(user);
                var result = await _context.SaveChangesAsync();
                return result > 0;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            var usernameLower = username.ToLower();
            return await _context.Users
                .FirstOrDefaultAsync(u => (u.UserName != null && u.UserName.ToLower() == usernameLower) ||
                                          (u.Email != null && u.Email.ToLower() == usernameLower));
        }

        public async Task UpdateAsync(User user)
        {
            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
