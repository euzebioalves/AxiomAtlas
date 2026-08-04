namespace Axiom.Atlas.Domain.Entities.ServiceDesk
{
    /// <summary>
    /// Image uploaded while documenting a GLPI ticket User Story.
    /// The image is kept locally so the Markdown can use a durable URL when it is sent to OpenProject.
    /// </summary>
    public class GlpiTicketWorkspaceImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid WorkspaceId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public GlpiTicketWorkspace Workspace { get; set; } = null!;
    }
}
