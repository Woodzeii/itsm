namespace itsm.Domain.Entities;

public class ServiceCatalog
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool? IsActive { get; set; } = true;

    public ServiceCatalog? Parent { get; set; }
    public ICollection<ServiceCatalog> Children { get; set; } = new List<ServiceCatalog>();
    public ICollection<FormTemplate> FormTemplates { get; set; } = new List<FormTemplate>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<KnowledgeBaseArticle> KnowledgeBaseArticles { get; set; } = new List<KnowledgeBaseArticle>();
}