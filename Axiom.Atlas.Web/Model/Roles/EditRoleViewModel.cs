namespace Axiom.Atlas.Web.Model.Roles
{
    public class EditRoleViewModel
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<RolePermissionViewModel> Permissions { get; set; } = new();
    }
}
