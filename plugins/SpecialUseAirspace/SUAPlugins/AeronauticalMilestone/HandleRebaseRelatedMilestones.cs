using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using static SUAPlugins.AeronauticalMilestone.Utilities;

namespace SUAPlugins.AeronauticalMilestone
{
    public class HandleRebaseRelatedMilestones : PluginBase
    {
        public HandleRebaseRelatedMilestones()
            : base(typeof(HandleRebaseRelatedMilestones))
        {
            // not implemented
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;

            if (
                context.MessageName != "Update"
                || context.PrimaryEntityName != "sua_aeronauticalmilestone"
            )
            {
                throw new InvalidPluginExecutionException(
                    $"Invalid registration for {nameof(HandleRebaseRelatedMilestones)}"
                );
            }

            if (!context.PostEntityImages.TryGetValue("PostImage", out Entity postImage))
            {
                throw new InvalidPluginExecutionException(
                    "Post image is missing in the plugin context."
                );
            }
            if (
                !postImage.TryGetAttributeValue(
                    "sua_aeronautical",
                    out EntityReference aeronauticalRef
                )
            )
            {
                throw new InvalidPluginExecutionException(
                    "Aeronautical reference is missing in the post image."
                );
            }
            try
            {
                var aeroNauticalQuery = new QueryExpression("sua_aeronauticalmilestone")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                aeronauticalRef.Id
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
                            Columns = new ColumnSet(true),
                            EntityAlias = "M"
                        }
                    }
                };
                var aeroMilestones = sysService.RetrieveMultiple(aeroNauticalQuery);

                tracer.Trace(
                    $"Found {aeroMilestones.Entities.Count} dependant milestones to evaluate for update."
                );

                var aeroMilestoneUpdates = RebaseRelatedMilestones(
                    postImage,
                    aeroMilestones,
                    tracer
                );

                if (aeroMilestoneUpdates.Entities.Count < 1)
                {
                    tracer.Trace("No dependant milestones found, exiting plugin.");
                    return;
                }

                tracer.Trace(
                    $"Preparing to update {aeroMilestoneUpdates.Entities.Count} dependant milestones with new anticipated dates."
                );

                var updateRequest = new UpdateMultipleRequest { Targets = aeroMilestoneUpdates };

                sysService.Execute(updateRequest);
                tracer.Trace(
                    $"Updated {aeroMilestoneUpdates.Entities.Count} dependant milestones with new anticipated dates."
                );

                UpdateLatestMilestoneBaseline(aeronauticalRef, sysService);
            }
            catch (Exception ex)
            {
                tracer.Trace($"Exception: {ex.Message}\n{ex.StackTrace}");
                throw new InvalidPluginExecutionException(
                    "An error occurred in HandleRebaseRelatedMilestones plugin.",
                    ex
                );
            }
        }
    }
}
