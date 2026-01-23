using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;
using static SUAPlugins.AeronauticalMilestone.Utilities;
using static SUAPlugins.Action.Utilities;
using System;
using Microsoft.Xrm.Sdk.Messages;

namespace SUAPlugins.Aeronautical
{
    public class HandleSupplementalRulemakingToggle : PluginBase
    {
        public HandleSupplementalRulemakingToggle()
            : base(typeof(HandleSupplementalRulemakingToggle))
        {
            // not implemented
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;

            if (context.MessageName != "Update" || context.PrimaryEntityName != "sua_aeronautical")
            {
                throw new InvalidPluginExecutionException(
                    $"Invalid registration for {nameof(HandleSupplementalRulemakingToggle)}"
                );
            }

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
            {
                throw new InvalidPluginExecutionException(
                    "Target entity is missing in the input parameters."
                );
            }
            try
            {
                if (
                    !target.TryGetAttributeValue(
                        "sua_requiressupplementalrulemaking",
                        out bool isSupplementalRulemaking
                    )
                )
                {
                    tracer.Trace("Supplemental Rulemaking not found in payload. Exiting");
                    return;
                }

                if (!context.PreEntityImages.TryGetValue("PreImage", out Entity preImage))
                {
                    throw new InvalidPluginExecutionException("PreImage is missing on update.");
                }
                if (!context.PostEntityImages.TryGetValue("PostImage", out Entity postImage))
                {
                    throw new InvalidPluginExecutionException("PostImage is missing on update.");
                }

                if (
                    !preImage.TryGetAttributeValue(
                        "sua_requiressupplementalrulemaking",
                        out bool wasSupplementalRulemaking
                    )
                )
                {
                    tracer.Trace(
                        "Supplemental Rulemaking not found in pre-image. Asserting false."
                    );
                }

                if (isSupplementalRulemaking == wasSupplementalRulemaking)
                {
                    tracer.Trace("Supplemental Rulemaking value has not changed. Exiting");
                    return;
                }

                var aeroMilestoneQuery = new QueryExpression("sua_aeronauticalmilestone")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                context.PrimaryEntityId
                            )
                        }
                    },
                    LinkEntities =
                    {
                        new LinkEntity(
                            "sua_aeronauticalmilestone",
                            "sua_milestone",
                            "sua_milestone",
                            "sua_milestoneid",
                            JoinOperator.Inner
                        )
                        {
                            EntityAlias = "M",
                            Columns = new ColumnSet(true),
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
                                    Orders =
                                    {
                                        new OrderExpression("sua_offset", OrderType.Ascending)
                                    }
                                }
                            }
                        }
                    }
                };

                EntityCollection aeroMilestones = sysService.RetrieveMultiple(aeroMilestoneQuery);
                if (aeroMilestones.Entities.Count == 0)
                {
                    tracer.Trace("No aeronautical milestones. Exiting");
                    return;
                }
                tracer.Trace(
                    $"Found {aeroMilestones.Entities.Count} aeronautical milestones associated with aeronautical ID: {context.PrimaryEntityId}"
                );
                var aeroMilestonesToRemove = aeroMilestones.Entities
                    .Where(ms =>
                    {
                        var supplementalType = ms.GetAttributeValue<AliasedValue>(
                            "M.sua_supplementaltype"
                        );
                        return supplementalType != null
                            && ((OptionSetValue)supplementalType.Value).Value
                                == (isSupplementalRulemaking ? 1 : 0);
                    })
                    .ToList();
                tracer.Trace(
                    $"Found {aeroMilestonesToRemove.Count()} aeronautical milestones to remove based on supplemental rulemaking toggle."
                );

                foreach (var milestone in aeroMilestonesToRemove)
                {
                    sysService.Delete("sua_aeronauticalmilestone", milestone.Id);
                    tracer.Trace(
                        $"Deleted aeronautical milestone with ID: {milestone.Id} due to supplemental rulemaking toggle."
                    );
                    // remove the milestone from aeroMilestones collection
                    aeroMilestones.Entities.Remove(milestone);
                }

                var action = GetActionFromAeronauticalRef(
                    target.ToEntityReference(),
                    sysService,
                    tracer
                );

                EntityCollection milestonesToAdd = GetRelevantMilestones(
                    action,
                    postImage,
                    preImage,
                    sysService,
                    tracer,
                    supplementalAugmentOnly: true
                );

                if (milestonesToAdd.Entities.Count == 0)
                {
                    tracer.Trace("No aeronautical milestones to add. Exiting");
                    return;
                }

                var latestBaselineDate = aeroMilestones.Entities
                    .Last()
                    .GetAttributeValue<DateTime>("sua_baseline");

                var currentBaselineDate = latestBaselineDate;
                int currentOffset = (int)
                    aeroMilestones.Entities
                        .Last()
                        .GetAttributeValue<AliasedValue>("MR.sua_offset")
                        .Value;

                var aeroMilestonesToCreate = new EntityCollection()
                {
                    EntityName = "sua_aeronauticalmilestone"
                };
                foreach (var milestone in milestonesToAdd.Entities)
                {
                    var offsetDays = (int)
                        milestone.GetAttributeValue<AliasedValue>("MR.sua_offset").Value;
                    var newBaselineDate = currentBaselineDate.AddDays(offsetDays - currentOffset);
                    var newAeroMilestone = new Entity("sua_aeronauticalmilestone")
                    {
                        ["sua_aeronautical"] = target.ToEntityReference(),
                        ["sua_milestone"] = milestone.ToEntityReference(),
                        ["sua_baseline"] = newBaselineDate,
                    };
                    aeroMilestonesToCreate.Entities.Add(newAeroMilestone);
                    tracer.Trace(
                        $"Added aeronautical milestone for milestone ID: {milestone.Id} with baseline date: {newBaselineDate.ToShortDateString()}"
                    );
                    currentBaselineDate = newBaselineDate;
                    currentOffset = offsetDays;
                }

                var createMultipleRequest = new CreateMultipleRequest
                {
                    Targets = aeroMilestonesToCreate
                };
                sysService.Execute(createMultipleRequest);
            }
            catch (Exception ex)
            {
                tracer.Trace(
                    $"Exception in {nameof(HandleSupplementalRulemakingToggle)}: {ex.Message}"
                );
                throw new InvalidPluginExecutionException(ex.Message, ex);
            }
        }
    }
}
