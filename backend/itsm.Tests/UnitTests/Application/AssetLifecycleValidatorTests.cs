using itsm.Application.Common.Models;
using itsm.Application.Common.Validators;
using itsm.Domain.Constants;

namespace itsm.Tests.UnitTests.Application;

public sealed class AssetLifecycleValidatorTests
{
    [Fact]
    public async Task CreateAssetRejectsCallerSelectedLifecycleStageAndAssignee()
    {
        var validator = new CreateAssetRequestValidator();

        var stageResult = await validator.ValidateAsync(new CreateAssetRequest
        {
            Name = "Актив",
            AssetClassId = 1,
            InventoryNumber = "INV-1",
            LifecycleStage = AssetLifecycleStages.InUse
        });
        var assigneeResult = await validator.ValidateAsync(new CreateAssetRequest
        {
            Name = "Актив",
            AssetClassId = 1,
            InventoryNumber = "INV-2",
            AssignedUserId = 7
        });

        Assert.False(stageResult.IsValid);
        Assert.False(assigneeResult.IsValid);
    }

    [Fact]
    public async Task AssetMovementValidatorRejectsUnlistedEmployeeTransfer()
    {
        var validator = new RegisterAssetMovementRequestValidator();

        var result = await validator.ValidateAsync(new RegisterAssetMovementRequest
        {
            MovementType = "transfer"
        });

        Assert.False(result.IsValid);
    }
}
