using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SUAPlugins.MilestoneRule
{
    public class ValidateSingleActiveMilestoneRule : PluginBase
    {
        public ValidateSingleActiveMilestoneRule()
            : base(typeof(ValidateSingleActiveMilestoneRule))
        {
            // not implemented
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            if (context.MessageName != "Create" && context.MessageName != "Update")
            {
                throw new InvalidPluginExecutionException(
                    "Invalid message registration for ValidateSingleActiveMilestoneRule"
                );
            }
            if (context.PrimaryEntityName != "sua_milestonerule")
            {
                throw new InvalidPluginExecutionException(
                    "Invalid message registration for ValidateSingleActiveMilestoneRule"
                );
            }
            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
                throw new InvalidPluginExecutionException("Target not found in context");
            try
            {
                if (!target.TryGetAttributeValue("statecode", out OptionSetValue stateCode))
                {
                    if (context.MessageName == "Create")
                    {
                        stateCode = new OptionSetValue(0);
                    }
                    else
                    {
                        return;
                    }
                }

                if (stateCode.Value != 0) // Not activating, so no need to validate
                    return;

                if (!target.TryGetAttributeValue("sua_milestone", out EntityReference milestoneRef))
                {
                    if (context.MessageName == "Create")
                    {
                        throw new InvalidPluginExecutionException("Milestone reference not found");
                    }
                    if (!context.PreEntityImages.TryGetValue("PreImage", out Entity preImage))
                        throw new InvalidPluginExecutionException("PreImage not found in context");
                    if (!preImage.TryGetAttributeValue("sua_milestone", out milestoneRef))
                    {
                        throw new InvalidPluginExecutionException("Milestone reference not found");
                    }
                }

                var milestoneRuleQuery = new QueryExpression("sua_milestonerule")
                {
                    ColumnSet = new ColumnSet("sua_milestoneruleid"),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_milestone",
                                ConditionOperator.Equal,
                                milestoneRef.Id
                            ),
                            new ConditionExpression(
                                "sua_milestoneruleid",
                                ConditionOperator.NotEqual,
                                target.Id
                            ),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0) // Active
                        }
                    }
                };

                var existingRules = sysService.RetrieveMultiple(milestoneRuleQuery);
                if (existingRules.Entities.Count > 0)
                {
                    throw new InvalidPluginExecutionException(
                        "Only one active Milestone Rule is allowed per Milestone."
                    );
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"Error in ValidateSingleActiveMilestoneRule: {ex.Message}");
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}
