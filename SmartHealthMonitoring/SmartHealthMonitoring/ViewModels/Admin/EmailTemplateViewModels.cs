using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels.Admin;

public class EmailTemplateDefinition
{
    public string TemplateName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DefaultSubject { get; set; } = string.Empty;

    public List<string> Tokens { get; set; } = new();

    public bool IsUsedInSystem { get; set; }
}

public class EmailTemplateListItemViewModel
{
    public string TemplateName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public DateTime? LastModifiedAt { get; set; }

    public long FileSize { get; set; }

    public int TokenCount { get; set; }

    public bool IsUsedInSystem { get; set; }
}

public class EmailTemplateEditViewModel
{
    [Required]
    public string TemplateName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề email.")]
    [StringLength(200, ErrorMessage = "Tiêu đề email không được vượt quá 200 ký tự.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung HTML của email.")]
    public string HtmlContent { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung email.")]
    public string BodyContent { get; set; } = string.Empty;

    public string EditorMode { get; set; } = "visual";

    public List<string> Tokens { get; set; } = new();

    public DateTime? LastModifiedAt { get; set; }

    public string PreviewHtml { get; set; } = string.Empty;

    public bool IsUsedInSystem { get; set; }
}
