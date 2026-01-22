using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SUAPlugins.Aeronautical
{
    public class InitializeMilestonesOnFormalProposalDateUpdate : PluginBase
    {
        public InitializeMilestonesOnFormalProposalDateUpdate()
            : base(typeof(InitializeMilestonesOnFormalProposalDateUpdate))
        {
            // not implemented
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;

            if (context.MessageName != "Update" && context.MessageName != "Create")
                return;

            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
                throw new InvalidPluginExecutionException("Target not found in context");

            if (!context.PreEntityImages.TryGetValue("PreImage", out Entity preImage))
                throw new InvalidPluginExecutionException("PreImage required on update");

            try
            {
                var formalProposalDate = Utilities.GetValueOnUpdate<DateTime>(
                    target,
                    preImage,
                    "sua_formalproposaldate"
                );
                if (formalProposalDate == default)
                {
                    throw new InvalidPluginExecutionException(
                        "Could not resolve Formal Proposal Date"
                    );
                }
                tracer.Trace($"Formal Proposal Date: {formalProposalDate}");

                var requiresSupplemental = Utilities.GetValueOnUpdate<bool>(
                    target,
                    preImage,
                    "sua_requiressupplemental"
                );

                tracer.Trace($"Requires Supplemental Rulemaking {requiresSupplemental}");

                var actionQuery = new QueryExpression("sua_action")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                target.Id
                            )
                        }
                    }
                };

                Entity action;
                try
                {
                    action =
                        sysService.RetrieveMultiple(actionQuery).Entities.FirstOrDefault()
                        ?? throw new InvalidPluginExecutionException("Related Action not found");
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }

                tracer.Trace($"Related Action: {action.Id}");

                if (!action.TryGetAttributeValue("sua_rulemaking", out bool isRulemaking))
                {
                    throw new InvalidPluginExecutionException("Rule Making not found on action");
                }

                tracer.Trace($"IsRulemaking: {isRulemaking}");

                if (!action.TryGetAttributeValue("sua_temporary", out bool isTemporary))
                {
                    throw new InvalidPluginExecutionException("Temporary not found on Action");
                }

                tracer.Trace($"IsTemporary: {isTemporary}");

                var milestoneQuery = new QueryExpression("sua_milestone")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
                            new ConditionExpression(
                                "sua_applicability",
                                ConditionOperator.In,
                                2,
                                isTemporary ? 0 : 1
                            ),
                            new ConditionExpression(
                                "sua_rulemakingtype",
                                ConditionOperator.In,
                                2,
                                isRulemaking ? 0 : 1
                            ),
                            new ConditionExpression(
                                "sua_supplementaltype",
                                ConditionOperator.In,
                                2,
                                requiresSupplemental ? 0 : 1
                            )
                        }
                    },
                    LinkEntities =
                    {
                        new LinkEntity(
                            "sua_milestone",
                            "sua_milestonerule",
                            "sua_milestoneid",
                            "sua_milestone",
                            JoinOperator.Inner
                        )
                        {
                            EntityAlias = "MR",
                            Columns = new ColumnSet(true),
                        }
                    }
                };

                EntityCollection milestones;
                try
                {
                    milestones = sysService.RetrieveMultiple(milestoneQuery);
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }

                tracer.Trace($"Found {milestones.Entities.Count} applicable milestones");

                if (milestones.Entities.Count == 0)
                {
                    return;
                }

                var aeronauticalMilestones = new EntityCollection()
                {
                    EntityName = "sua_aeronauticalmilestone"
                };

                foreach (var milestone in milestones.Entities)
                {
                    var relatedMilestone = new Entity(aeronauticalMilestones.EntityName)
                    {
                        ["sua_milestone"] = milestone.ToEntityReference(),
                        ["sua_aeronautical"] = target.ToEntityReference(),
                        ["sua_baseline"] = formalProposalDate.AddDays(
                            (int)milestone.GetAttributeValue<AliasedValue>("MR.sua_offset").Value
                        )
                    };
                    aeronauticalMilestones.Entities.Add(relatedMilestone);
                }

                var createRequest = new CreateMultipleRequest()
                {
                    Targets = aeronauticalMilestones
                };

                try
                {
                    sysService.Execute(createRequest);
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }

                tracer.Trace("Initial Milestones related");
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message, ex);
            }
        }
    }
}
