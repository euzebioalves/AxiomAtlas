namespace Axiom.Atlas.Application.DTOs.Roles
{
    public class RolePermissionDto
    {
        public string Value { get; set; } = string.Empty; // Ex: "Permissions.Users.Create"
        public bool Selected { get; set; } // true se o papel tiver essa permissão
    }
}
