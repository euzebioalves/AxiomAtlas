namespace Axiom.Atlas.Application.DTOs.Roles
{
    public class UpdateRolePermissionsDto
    {
        public string RoleId { get; set; } = string.Empty;
        public List<RolePermissionDto> Permissions { get; set; } = new();
    }
}
