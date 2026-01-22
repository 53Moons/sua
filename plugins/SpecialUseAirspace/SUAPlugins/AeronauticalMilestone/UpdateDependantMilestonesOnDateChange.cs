using Microsoft.Xrm.Sdk;

namespace SUAPlugins.AeronauticalMilestone
{
    public class UpdateDependantMilestonesOnDateChange : PluginBase
    {
        public UpdateDependantMilestonesOnDateChange()
            : base(typeof(UpdateDependantMilestonesOnDateChange))
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
        }
    }
}
