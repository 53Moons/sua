using Microsoft.Xrm.Sdk;
using System;
using static SUAPlugins.AeronauticalMilestone.Utilities;

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
            {
                throw new InvalidPluginExecutionException(
                    "Invalid message registration for InitializeMilestonesOnFormalProposalDateUpdate"
                );
            }

            if (context.PrimaryEntityName != "sua_aeronautical")
            {
                throw new InvalidPluginExecutionException(
                    "Invalid message registration for InitializeMilestonesOnFormalProposalDateUpdate"
                );
            }

            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
                throw new InvalidPluginExecutionException("Target not found in context");

            if (!context.PreEntityImages.TryGetValue("PreImage", out Entity preImage))
                throw new InvalidPluginExecutionException("PreImage required on update");

            try
            {
                InitializeMilestones(target, preImage, sysService, tracer);
                UpdateLatestMilestoneBaseline(target.ToEntityReference(), sysService);
            }
            catch (Exception ex)
            {
                tracer.Trace(
                    $"Error in InitializeMilestonesOnFormalProposalDateUpdate: {ex.Message}"
                );
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}
