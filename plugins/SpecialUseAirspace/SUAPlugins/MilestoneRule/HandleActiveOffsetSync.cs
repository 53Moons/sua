using Microsoft.Xrm.Sdk;
using System;

namespace SUAPlugins.MilestoneRule
{
    public class HandleActiveOffsetSync : PluginBase
    {
        public HandleActiveOffsetSync()
            : base(typeof(HandleActiveOffsetSync))
        {
            // not implemented
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            if (
                (context.MessageName != "Create" && context.MessageName != "Update")
                || context.Stage != 40
                || context.PrimaryEntityName != "sua_milestonerule"
            )
            {
                throw new InvalidPluginExecutionException(
                    "Invalid registration for HandleActiveOffsetSync"
                );
            }
            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;
            if (!context.InputParameters.TryGetValue("Target", out Entity target))
                throw new InvalidPluginExecutionException("Target not found in context");
            if (!context.PostEntityImages.TryGetValue("PostImage", out Entity postImage))
                throw new InvalidPluginExecutionException("PreImage required on update");
            try
            {
                if (!postImage.TryGetAttributeValue("statecode", out OptionSetValue stateCode))
                    throw new InvalidPluginExecutionException("statecode not found on post image");

                if (stateCode.Value != 0)
                    return;

                if (
                    !postImage.TryGetAttributeValue(
                        "sua_milestone",
                        out EntityReference milestoneRef
                    )
                )
                    throw new InvalidPluginExecutionException(
                        "sua_milestone not found on post image"
                    );

                if (!postImage.TryGetAttributeValue("sua_offset", out int offset))
                    throw new InvalidPluginExecutionException("sua_offset not found on post image");

                var update = new Entity(milestoneRef.LogicalName, milestoneRef.Id)
                {
                    ["sua_activeoffset"] = offset
                };
                tracer.Trace($"Updating milestone {milestoneRef.Id} with active offset {offset}");
                sysService.Update(update);
            }
            catch (Exception ex)
            {
                tracer.Trace($"Exception in HandleActiveOffsetSync: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
    }
}
