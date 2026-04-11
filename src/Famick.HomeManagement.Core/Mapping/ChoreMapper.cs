#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Chores;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class ChoreMapper
{
    public static ChoreDto ToDto(Chore source)
    {
        var dto = ToDtoPartial(source);
        dto.NextExecutionAssignedToUserName = source.NextExecutionAssignedToUser != null
            ? $"{source.NextExecutionAssignedToUser.FirstName} {source.NextExecutionAssignedToUser.LastName}".Trim()
            : null;
        dto.ProductName = source.Product != null ? source.Product.Name : null;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ChoreDto.NextExecutionAssignedToUserName))]
    [MapperIgnoreTarget(nameof(ChoreDto.ProductName))]
    [MapperIgnoreTarget(nameof(ChoreDto.NextExecutionDate))]
    private static partial ChoreDto ToDtoPartial(Chore source);

    public static ChoreSummaryDto ToSummaryDto(Chore source)
    {
        var dto = ToSummaryDtoPartial(source);
        dto.AssignedToUserName = source.NextExecutionAssignedToUser != null
            ? $"{source.NextExecutionAssignedToUser.FirstName} {source.NextExecutionAssignedToUser.LastName}".Trim()
            : null;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ChoreSummaryDto.AssignedToUserName))]
    [MapperIgnoreTarget(nameof(ChoreSummaryDto.NextExecutionDate))]
    [MapperIgnoreTarget(nameof(ChoreSummaryDto.IsOverdue))]
    private static partial ChoreSummaryDto ToSummaryDtoPartial(Chore source);

    [MapperIgnoreTarget(nameof(Chore.Id))]
    [MapperIgnoreTarget(nameof(Chore.TenantId))]
    [MapperIgnoreTarget(nameof(Chore.CreatedAt))]
    [MapperIgnoreTarget(nameof(Chore.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Chore.NextExecutionAssignedToUserId))]
    [MapperIgnoreTarget(nameof(Chore.EquipmentId))]
    [MapperIgnoreTarget(nameof(Chore.Product))]
    [MapperIgnoreTarget(nameof(Chore.NextExecutionAssignedToUser))]
    [MapperIgnoreTarget(nameof(Chore.Equipment))]
    [MapperIgnoreTarget(nameof(Chore.LogEntries))]
    public static partial Chore FromCreateRequest(CreateChoreRequest source);

    [MapperIgnoreTarget(nameof(Chore.Id))]
    [MapperIgnoreTarget(nameof(Chore.TenantId))]
    [MapperIgnoreTarget(nameof(Chore.CreatedAt))]
    [MapperIgnoreTarget(nameof(Chore.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Chore.NextExecutionAssignedToUserId))]
    [MapperIgnoreTarget(nameof(Chore.EquipmentId))]
    [MapperIgnoreTarget(nameof(Chore.Product))]
    [MapperIgnoreTarget(nameof(Chore.NextExecutionAssignedToUser))]
    [MapperIgnoreTarget(nameof(Chore.Equipment))]
    [MapperIgnoreTarget(nameof(Chore.LogEntries))]
    public static partial void Update(UpdateChoreRequest source, Chore target);

    public static ChoreLogDto ToLogDto(ChoreLog source)
    {
        var dto = ToLogDtoPartial(source);
        dto.ChoreName = source.Chore != null ? source.Chore.Name : string.Empty;
        dto.DoneByUserName = source.DoneByUser != null
            ? $"{source.DoneByUser.FirstName} {source.DoneByUser.LastName}".Trim()
            : null;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ChoreLogDto.ChoreName))]
    [MapperIgnoreTarget(nameof(ChoreLogDto.DoneByUserName))]
    private static partial ChoreLogDto ToLogDtoPartial(ChoreLog source);
}
