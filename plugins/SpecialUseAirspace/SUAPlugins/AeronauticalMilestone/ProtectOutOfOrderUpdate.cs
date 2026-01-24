using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SUAPlugins.AeronauticalMilestone
{
    public class ProtectOutOfOrderUpdate : PluginBase
    {
        public ProtectOutOfOrderUpdate()
            : base(typeof(ProtectOutOfOrderUpdate))
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

                if (!target.TryGetAttributeValue("sua_baseline", out DateTime baselineDate))
                {
                    if (!preImage.TryGetAttributeValue("sua_baseline", out baselineDate))
                    {
                        throw new InvalidPluginExecutionException(
                            "Baseline date (sua_baseline) not found on Target or PreImage."
                        );
                    }
                }

                if (
                    !target.TryGetAttributeValue(
                        "sua_aeronautical",
                        out EntityReference aeronauticalRef
                    )
                )
                {
                    if (!preImage.TryGetAttributeValue("sua_aeronautical", out aeronauticalRef))
                    {
                        throw new InvalidPluginExecutionException(
                            "Aeronautical reference (sua_aeronautical) not found on Target or PreImage."
                        );
                    }
                }

                var aeroMilestoneQuery = new QueryExpression("sua_aeronauticalmilestone")
                {
                    ColumnSet = new ColumnSet("sua_aeronauticalmilestoneid", "sua_datecompleted"),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_aeronauticalmilestoneid",
                                ConditionOperator.NotEqual,
                                target.Id
                            ),
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                aeronauticalRef.Id
                            ),
                            new ConditionExpression(
                                "sua_baseline",
                                ConditionOperator.LessEqual,
                                baselineDate
                            ),
                            new ConditionExpression("sua_datecompleted", ConditionOperator.Null)
                        }
                    }
                };

                var milestones = sysService.RetrieveMultiple(aeroMilestoneQuery);
                if (milestones.Entities.Count > 0)
                {
                    throw new InvalidPluginExecutionException(
                        "Cannot complete this milestone while prior milestones remain incomplete."
                    );
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"Error in ProtectOutOfOrderUpdate: {ex.Message}");
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}
