using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using static SUAPlugins.AeronauticalMilestone.Utilities;
using static SUAPlugins.Utilities;

namespace SUAPlugins.Action
{
    public class InitializeMilestonesOnUpdate : PluginBase
    {
        public InitializeMilestonesOnUpdate()
            : base(typeof(InitializeMilestonesOnUpdate))
        {
            // not implemented
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;

            if (context.MessageName != "Update")
            {
                throw new InvalidPluginExecutionException(
                    "Invalid message registration for InitializeMilestonesOnUpdate"
                );
            }

            if (context.PrimaryEntityName != "sua_action")
            {
                throw new InvalidPluginExecutionException(
                    "Invalid message registration for InitializeMilestonesOnUpdate"
                );
            }

            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
                throw new InvalidPluginExecutionException("Target not found in context");

            if (!context.PreEntityImages.TryGetValue("PreImage", out Entity preImage))
                throw new InvalidPluginExecutionException("PreImage required on update");

            Entity aero;
            try
            {
                var aeronauticalRef = GetValueOnUpdate<EntityReference>(
                    target,
                    preImage,
                    "sua_aeronautical"
                );

                aero = sysService.Retrieve(
                    "sua_aeronautical",
                    aeronauticalRef.Id,
                    new ColumnSet(true)
                );

                InitializeMilestones(aero, aero, sysService, tracer);
            }
            catch (Exception ex)
            {
                tracer.Trace($"Error in InitializeMilestonesOnUpdate: {ex.Message}");
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}
