using Axiom.Atlas.Application.DTOs.Users;
using Axiom.Atlas.Domain.Entities.Users;
using Axiom.Atlas.API.Models.Users;
using Axiom.Atlas.Domain.Interfaces.Users;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;

namespace Axiom.Atlas.API.Controllers.Users
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IUserRepository _userRepository;
        private readonly AppDbContext _context;

        public UsersController(UserManager<User> user, RoleManager<IdentityRole<Guid>> roleManager, IUserRepository userRepository, AppDbContext context)
        {
            _userManager = user;
            _roleManager = roleManager;
            _userRepository = userRepository;
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet("profile-picture/{username}")]
        public async Task<IActionResult> GetProfilePicture(string username)
        {
            var user = await _userRepository.GetByUsernameAsync(username);

            if (user == null || user.ProfilePicture == null || user.ProfilePicture.Length == 0)
            {
                return NotFound();
            }

            return File(user.ProfilePicture, "image/jpeg");
        }

        [HttpPut("update-profile")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário autenticado não identificado." });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.FullName = request.FullName ?? user.FullName;

            if (request.ProfilePictureFile != null && request.ProfilePictureFile.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await request.ProfilePictureFile.CopyToAsync(memoryStream);

                user.ProfilePicture = memoryStream.ToArray();
            }

            await _userRepository.UpdateAsync(user);

            return Ok(new { Message = "Perfil atualizado com sucesso!" });
        }

        // 1. LISTAR TODOS
        [HttpGet]
        [Authorize(Policy = "AdministrationOnly")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .Select(u => new
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    UserName = u.UserName,
                    JobTitle = u.JobTitle,
                    ProfilePictureUrl = u.ProfilePicture,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            var userIds = users.Select(u => u.Id).ToList();
            var userRoles = await (from userRole in _context.UserRoles.AsNoTracking()
                                   join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                                   where userIds.Contains(userRole.UserId)
                                   select new
                                   {
                                       userRole.UserId,
                                       RoleName = role.Name
                                   })
                .ToListAsync();

            var rolesByUserId = userRoles
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join(", ", group.Select(x => x.RoleName).Where(roleName => !string.IsNullOrWhiteSpace(roleName))));

            var result = users.Select(user =>
            {
                rolesByUserId.TryGetValue(user.Id, out var roleName);

                return new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.UserName,
                    user.JobTitle,
                    user.ProfilePictureUrl,
                    user.IsActive,
                    Role = string.IsNullOrWhiteSpace(roleName) ? "Sem perfil" : roleName
                };
            });

            return Ok(result);
        }

        // 2. OBTER POR ID
        [HttpGet("{id}")]
        [Authorize(Policy = "AdministrationOnly")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            // Precisamos descobrir qual é o Perfil (Role) atual dele para preencher o <select>
            var roles = await _userManager.GetRolesAsync(user);
            var roleName = roles.FirstOrDefault();

            Guid? roleId = null;
            if (!string.IsNullOrEmpty(roleName))
            {
                var role = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                roleId = role?.Id;
            }

            // Montamos um objeto anônimo (ou você pode criar um DTO de retorno) com os dados exatos
            var userData = new
            {
                user.Id,
                user.FullName,
                user.UserName,
                user.Email,
                user.JobTitle,
                user.PhoneNumber,
                user.ProfilePicture,
                user.IsActive,
                RoleId = roleId
            };

            return Ok(userData);
        }

        // 3. CRIAR NOVO USUÁRIO (A Mágica do Identity)
        [HttpPost]
        [Authorize(Policy = "AdministrationOnly")]
        public async Task<IActionResult> Create([FromBody] UserCreateDto dto)
        {
            // 1. Validações básicas
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "E-mail e senha são obrigatórios." });

            // 2. Busca o perfil (Role) pelo ID para garantir que ele existe e pegar o Nome
            //var role = await _roleManager.FindByIdAsync(dto.Role.ToString());
            var role = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId);
            if (role == null)
                return BadRequest(new { message = "O perfil selecionado não existe no sistema." });

            // 3. Instancia o novo usuário (Substitua 'User' pelo nome da sua classe caso seja diferente, ex: ApplicationUser)
            var user = new User
            {
                UserName = dto.UserName,
                Email = dto.Email,
                FullName = dto.FullName,
                JobTitle = dto.JobTitle,
                PhoneNumber = dto.PhoneNumber,
                ProfilePicture = dto.ProfilePicture,
                EmailConfirmed = true
            };

            // 4. Cria o usuário (O Identity já aplica o Hash na senha automaticamente!)
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (result.Succeeded)
            {
                var roleName = role.Name ?? throw new InvalidOperationException("O perfil selecionado está sem nome.");

                // 5. Vincula o usuário ao perfil 
                // O Identity exige o NOME do papel para fazer o vínculo, por isso buscamos ele no passo 2
                var roleResult = await _userManager.AddToRoleAsync(user, roleName);

                if (roleResult.Succeeded)
                    return Ok(new { message = "Usuário criado com sucesso!" });

                return BadRequest(new { message = "Usuário criado, mas houve um erro ao atribuir o perfil." });
            }

            // Se falhar (ex: senha fraca, e-mail já existe), devolvemos os erros nativos do Identity
            var erros = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(new { message = "Erro ao criar usuário.", errors = erros });
        }

        // 4. ATUALIZAR USUÁRIO
        [HttpPut("{id}")]
        [Authorize(Policy = "AdministrationOnly")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UserUpdateDto dto)
        {
            if (id != dto.Id)
                return BadRequest(new { message = "O ID da URL não confere com o ID do corpo da requisição." });

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            var newRole = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId);
            if (newRole == null || string.IsNullOrWhiteSpace(newRole.Name))
            {
                return BadRequest(new { message = "O perfil selecionado não existe ou está inválido." });
            }

            // Atualiza os dados de texto
            user.FullName = dto.FullName;
            user.UserName = dto.UserName;
            user.Email = dto.Email;
            user.JobTitle = dto.JobTitle;
            user.PhoneNumber = dto.PhoneNumber;

            // Só sobrescreve a foto de perfil se o front-end mandar uma nova string Base64
            if (dto.ProfilePicture is { Length: > 0 })
            {
                user.ProfilePicture = dto.ProfilePicture;
            }

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                var erros = updateResult.Errors.Select(e => e.Description).ToList();
                return BadRequest(new { message = "Erro ao atualizar dados básicos.", errors = erros });
            }

            // Atualiza o Perfil (Role) sem deixar o usuário sem perfil caso alguma etapa falhe.
            var currentRoles = await _userManager.GetRolesAsync(user);
            var newRoleName = newRole.Name;

            if (!currentRoles.Contains(newRoleName))
            {
                var addRoleResult = await _userManager.AddToRoleAsync(user, newRoleName);
                if (!addRoleResult.Succeeded)
                {
                    var erros = addRoleResult.Errors.Select(e => e.Description).ToList();
                    return BadRequest(new { message = "Dados básicos atualizados, mas houve erro ao atribuir o novo perfil.", errors = erros });
                }
            }

            var rolesToRemove = currentRoles
                .Where(role => !role.Equals(newRoleName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rolesToRemove.Count > 0)
            {
                var removeRoleResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeRoleResult.Succeeded)
                {
                    var erros = removeRoleResult.Errors.Select(e => e.Description).ToList();
                    return BadRequest(new { message = "Dados básicos atualizados, mas houve erro ao remover perfis antigos.", errors = erros });
                }
            }

            return Ok(new { message = "Usuário atualizado com sucesso!" });
        }

        // 5. EXCLUIR / INATIVAR
        [HttpDelete("{id}")]
        [Authorize(Policy = "AdministrationOnly")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            // Proteção extra: Evitar que o administrador principal exclua a si mesmo (Opcional, mas recomendado)
            // Se o ID for igual ao ID do usuário logado no token, você pode barrar aqui.

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
                return Ok(new { message = "Usuário excluído com sucesso!" });

            var erros = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(new { message = "Erro ao excluir o usuário.", errors = erros });
        }

        [HttpPut("{id}/ToggleStatus")]
        [Authorize(Policy = "AdministrationOnly")]
        public async Task<IActionResult> ToggleStatus(Guid id, [FromServices] AppDbContext context)
        {
            // O UserManager busca o usuário (e o Entity Framework já começa a vigiá-lo)
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            // 1. Inverte o status atual
            user.IsActive = !user.IsActive;

            // 2. Aplica ou remove o bloqueio
            if (!user.IsActive)
            {
                user.LockoutEnd = DateTimeOffset.MaxValue;
            }
            else
            {
                user.LockoutEnd = null;
            }

            // 3. REGRA DE SEGURANÇA DO IDENTITY: 
            // Como vamos pular o UserManager, precisamos girar a chave de concorrência manualmente
            user.ConcurrencyStamp = Guid.NewGuid().ToString();

            // 4. A MÁGICA CIRÚRGICA: 
            // Salvamos direto pelo Context. O EF Core vai comparar o objeto e ver que 
            // APENAS IsActive, LockoutEnd e ConcurrencyStamp mudaram!
            await context.SaveChangesAsync();

            var statusName = user.IsActive ? "ativado" : "inativado";
            return Ok(new { message = $"Usuário {statusName} com sucesso!", isActive = user.IsActive });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            // Pega o ID do usuário logado pelo Token
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado ou token inválido." });
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null) return NotFound("Usuário não encontrado.");

            // O UserManager já faz todo o trabalho pesado e criptografia para nós!
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (result.Succeeded)
            {
                return Ok(new { message = "Senha alterada com sucesso." });
            }

            return BadRequest(result.Errors);
        }
    }
}
