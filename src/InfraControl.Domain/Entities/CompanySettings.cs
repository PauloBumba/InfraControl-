using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Configuração institucional da empresa (spec evolução 2026-11-07 §3) —
/// singleton: uma única linha alimenta relatórios e documentos gerados,
/// eliminando dados fixos no código. O logo é apenas o path web do arquivo
/// enviado pelo endpoint genérico de uploads.
/// </summary>
public sealed class CompanySettings : AggregateRoot
{
    public const int NameMaxLength = 200;
    public const int LegalNameMaxLength = 200;
    public const int DocumentMaxLength = 32;
    public const int ContactMaxLength = 200;
    public const int AddressMaxLength = 200;
    public const int ZipCodeMaxLength = 20;
    public const int LogoPathMaxLength = 500;

    /// <summary>Nome de exibição da empresa (usado em relatórios e UI).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Razão social / nome jurídico.</summary>
    public string? LegalName { get; private set; }

    /// <summary>CNPJ (somente dígitos ou formatado — validado no handler).</summary>
    public string? Cnpj { get; private set; }

    /// <summary>Path web do logotipo (ex.: /uploads/company/abc.png).</summary>
    public string? LogoPath { get; private set; }

    public string? Street { get; private set; }
    public string? Number { get; private set; }
    public string? Complement { get; private set; }
    public string? District { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? ZipCode { get; private set; }

    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }

    private readonly List<CompanyDepartment> _departments = [];
    public IReadOnlyCollection<CompanyDepartment> Departments => _departments.AsReadOnly();

    private CompanySettings() { }

    public static CompanySettings Create(string name) =>
        new() { Name = Require(name, NameMaxLength, "O nome da empresa é obrigatório.", nameof(name)) };

    /// <summary>Upsert completo — substitui todos os campos e departamentos.</summary>
    public void Update(
        string name,
        string? legalName,
        string? cnpj,
        string? logoPath,
        string? street,
        string? number,
        string? complement,
        string? district,
        string? city,
        string? state,
        string? zipCode,
        string? phone,
        string? email,
        string? website,
        IReadOnlyCollection<CompanyDepartment> departments)
    {
        Name = Require(name, NameMaxLength, "O nome da empresa é obrigatório.", nameof(name));
        LegalName = Optional(legalName, LegalNameMaxLength, nameof(legalName));
        Cnpj = Optional(cnpj, DocumentMaxLength, nameof(cnpj));
        LogoPath = Optional(logoPath, LogoPathMaxLength, nameof(logoPath));
        Street = Optional(street, AddressMaxLength, nameof(street));
        Number = Optional(number, 20, nameof(number));
        Complement = Optional(complement, AddressMaxLength, nameof(complement));
        District = Optional(district, AddressMaxLength, nameof(district));
        City = Optional(city, AddressMaxLength, nameof(city));
        State = Optional(state, 40, nameof(state));
        ZipCode = Optional(zipCode, ZipCodeMaxLength, nameof(zipCode));
        Phone = Optional(phone, ContactMaxLength, nameof(phone));
        Email = Optional(email, ContactMaxLength, nameof(email));
        Website = Optional(website, ContactMaxLength, nameof(website));

        _departments.Clear();
        foreach (var department in departments)
        {
            department.CompanySettingsId = Id;
            _departments.Add(department);
        }

        Touch();
    }

    private static string Require(string? value, int max, string message, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, paramName);

        value = value.Trim();
        if (value.Length > max)
            throw new ArgumentException($"O campo excede o limite de {max} caracteres.", paramName);

        return value;
    }

    private static string? Optional(string? value, int max, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.Length > max)
            throw new ArgumentException($"O campo excede o limite de {max} caracteres.", paramName);

        return value;
    }
}
