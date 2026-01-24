using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using static SUAPlugins.Utilities;

namespace SUAPlugins.AeronauticalMilestone
{
    public static class Utilities
    {
        public static void RemoveExistingAeronauticalMilestones(
            EntityReference aeronauticalRef,
            IOrganizationService service
        )
        {
            if (aeronauticalRef == null)
                throw new InvalidPluginExecutionException("Aeronautical reference is null");
            if (service == null)
                throw new InvalidPluginExecutionException("IOrganizationService is null");
            try
            {
                var query = new QueryExpression("sua_aeronauticalmilestone")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                aeronauticalRef.Id
                            )
                        }
                    }
                };
                var existingMilestones = service.RetrieveMultiple(query).Entities;
                if (existingMilestones.Count == 0)
                    return;

                foreach (var milestone in existingMilestones)
                {
                    service.Delete(milestone.LogicalName, milestone.Id);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error removing existing aeronautical milestones: " + ex.Message
                );
            }
        }

        public static EntityCollection GetRelevantMilestones(
            Entity action,
            Entity aero,
            Entity preAero,
            IOrganizationService service,
            ITracingService tracer,
            bool supplementalAugmentOnly = false
        )
        {
            EntityCollection milestones;
            try
            {
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

                var requiresSupplemental = GetValueOnUpdate<bool>(
                    aero,
                    preAero,
                    "sua_requiressupplementalrulemaking"
                );
                tracer.Trace($"Requires Supplemental Rulemaking {requiresSupplemental}");

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
                            LinkCriteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                                }
                            },
                            Orders = { new OrderExpression("sua_offset", OrderType.Ascending) }
                        }
                    }
                };
                if (supplementalAugmentOnly)
                {
                    milestoneQuery.Criteria.AddCondition(
                        new ConditionExpression(
                            "sua_supplementaltype",
                            ConditionOperator.Equal,
                            requiresSupplemental ? 0 : 1
                        )
                    );
                }
                else
                {
                    milestoneQuery.Criteria.AddCondition(
                        new ConditionExpression(
                            "sua_supplementaltype",
                            ConditionOperator.In,
                            2,
                            requiresSupplemental ? 0 : 1
                        )
                    );
                }

                try
                {
                    milestones = service.RetrieveMultiple(milestoneQuery);
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }

                tracer.Trace($"Found {milestones.Entities.Count} applicable milestones");
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }

            return milestones;
        }

        public static void InitializeMilestones(
            Entity aero,
            Entity preAero,
            IOrganizationService service,
            ITracingService tracer
        )
        {
            try
            {
                RemoveExistingAeronauticalMilestones(aero.ToEntityReference(), service);
                var formalProposalDate = GetValueOnUpdate<DateTime>(
                    aero,
                    preAero,
                    "sua_formalproposaldate"
                );
                if (formalProposalDate == default)
                {
                    tracer.Trace("Formal Proposal Date not found, skipping milestone creation");
                    return;
                }
                tracer.Trace($"Formal Proposal Date: {formalProposalDate}");

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
                                aero.Id
                            )
                        }
                    }
                };

                Entity action;
                try
                {
                    action =
                        service.RetrieveMultiple(actionQuery).Entities.FirstOrDefault()
                        ?? throw new InvalidPluginExecutionException("Related Action not found");
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }

                tracer.Trace($"Related Action: {action.Id}");

                EntityCollection milestones = GetRelevantMilestones(
                    action,
                    aero,
                    preAero,
                    service,
                    tracer
                );

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
                        ["sua_aeronautical"] = aero.ToEntityReference(),
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
                    service.Execute(createRequest);
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
