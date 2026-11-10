namespace InfraControl.Api.ViewModels.Company;

public sealed class CompanyDepartmentViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Responsible { get; set; }
}

public sealed class UpsertCompanySettingsViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? Cnpj { get; set; }
    public string? LogoPath { get; set; }
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public List<CompanyDepartmentViewModel>? Departments { get; set; }
}
