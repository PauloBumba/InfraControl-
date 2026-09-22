using System.ComponentModel.DataAnnotations;
using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Admin;

public sealed record RoleViewModel([property: EnumDataType(typeof(UserRole))] UserRole Role);
