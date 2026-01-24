using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using SUAPlugins.Aeronautical;
using static SUAPlugins.AeronauticalMilestone.Utilities;

namespace SUAPlugins.AeronauticalMilestone
{
    public class UpdateDependantMiletonesOnDelayChange : PluginBase
    {
        public UpdateDependantMiletonesOnDelayChange()
            : base(typeof(UpdateDependantMiletonesOnDelayChange))
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
                    $"Invalid registration for {nameof(HandleSupplementalRulemakingToggle)}"
                );
            }

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
            {
                throw new InvalidPluginExecutionException(
                    "Target entity is missing in the input parameters."
                );
            }

            if (!target.TryGetAttributeValue("sua_anticipateddelay", out int newDelay))
            {
                tracer.Trace("No anticipated delay change detected, exiting plugin.");
                return;
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

            var aeroMilestoneUpdates = RebaseRelatedMilestones(postImage, aeroMilestones, tracer);

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
        }
    }
}
