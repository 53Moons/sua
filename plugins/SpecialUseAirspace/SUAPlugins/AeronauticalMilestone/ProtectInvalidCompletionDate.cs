using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using static SUAPlugins.Utilities;

namespace SUAPlugins.AeronauticalMilestone
{
    public class ProtectInvalidCompletionDate : PluginBase
    {
        public ProtectInvalidCompletionDate()
            : base(typeof(ProtectInvalidCompletionDate))
        {
            // not implemented
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            if (context.MessageName != "Update")
                return;
            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;
            if (!context.InputParameters.TryGetValue("Target", out Entity target))
                throw new InvalidPluginExecutionException("Target not found in context");
            if (!context.PreEntityImages.TryGetValue("PreImage", out Entity preImage))
                throw new InvalidPluginExecutionException("PreImage required on update");
            try
            {
                if (!target.TryGetAttributeValue("sua_datecompleted", out DateTime completionDate))
                    return;

                var baselineDate = GetValueOnUpdate<DateTime>(target, preImage, "sua_baseline");
                if (baselineDate == DateTime.MinValue)
                    throw new InvalidPluginExecutionException("Baseline date is required.");

                var aeroRef =
                    GetValueOnUpdate<EntityReference>(target, preImage, "sua_aeronautical")
                    ?? throw new InvalidPluginExecutionException(
                        "Aeronautical reference is required."
                    );
                var aeroMilestoneQuery = new QueryExpression("sua_aeronauticalmilestone")
                {
                    ColumnSet = new ColumnSet("sua_baseline", "sua_datecompleted"),
                    Orders = { new OrderExpression("sua_baseline", OrderType.Ascending) },
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_baseline",
                                ConditionOperator.LessEqual,
                                baselineDate
                            ),
                            new ConditionExpression("sua_datecompleted", ConditionOperator.NotNull),
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                aeroRef.Id
                            )
                        }
                    }
                };
                var aeroMilestoneRecords = sysService.RetrieveMultiple(aeroMilestoneQuery);
                if (aeroMilestoneRecords.Entities.Count == 0)
                    return;
                var latestMilestone = aeroMilestoneRecords.Entities[
                    aeroMilestoneRecords.Entities.Count - 1
                ];
                var latestCompletionDate = latestMilestone.GetAttributeValue<DateTime>(
                    "sua_datecompleted"
                );
                tracer.Trace(
                    $"Latest completed milestone date: {latestCompletionDate}, New completion date: {completionDate}"
                );
                if (completionDate < latestCompletionDate)
                {
                    throw new InvalidPluginExecutionException(
                        "Completion Date cannot be earlier than the latest completed milestone's Completion Date."
                    );
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"Error in ProtectInvalidCompletionDate: {ex.Message}");
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}
