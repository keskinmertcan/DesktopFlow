using DesktopFlow.Core.Models;

namespace DesktopFlow.Core.Safety;

public static class RecoveryPlanner
{
    public static RecoveryDecision Decide(bool sourceExists, bool destinationExists, bool tempExists)
    {
        if (tempExists && !sourceExists && !destinationExists)
        {
            return new RecoveryDecision(
                RecoveryAction.PromoteTempAndSucceed,
                "An interrupted copy was completed from the temporary file.");
        }

        if (tempExists && sourceExists)
        {
            return new RecoveryDecision(
                RecoveryAction.DeleteTempAndFail,
                "An interrupted copy was removed. The original file was kept.");
        }

        if (!sourceExists && destinationExists)
        {
            return new RecoveryDecision(RecoveryAction.MarkSucceeded, null);
        }

        if (sourceExists && destinationExists)
        {
            return new RecoveryDecision(
                RecoveryAction.MarkFailed,
                "The move was interrupted while both copies existed. Neither file was deleted.");
        }

        if (sourceExists)
        {
            return new RecoveryDecision(
                RecoveryAction.MarkFailed,
                "The move was interrupted before the file was moved. Nothing was changed.");
        }

        return new RecoveryDecision(
            RecoveryAction.MarkFailed,
            "The move was interrupted and the file could not be found.");
    }
}
