using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels.Admin;

public class EmailTemplateDefinition
{
    public string TemplateName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DefaultSubject { get; set; } = string.Empty;

}

public class EmailTemplateListItemViewModel
{
    public string TemplateName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public DateTime? LastModifiedAt { get; set; }

    public long FileSize { get; set; }

}

public class EmailTemplateEditViewModel
{
    [Required]
    public string TemplateName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p ti�u d? email.")]
    [StringLength(200, ErrorMessage = "Ti�u d? email kh�ng du?c vu?t qu� 200 k� t?.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p n?i dung HTML c?a email.")]
    public string HtmlContent { get; set; } = string.Empty;

    public List<string> Tokens { get; set; } = new();

    public DateTime? LastModifiedAt { get; set; }

}
