using Microsoft.Xrm.Sdk;
using System;

namespace SUAPlugins.Action
{
    public class InitializeRelatedRecordOnCreate : PluginBase
    {
        public InitializeRelatedRecordOnCreate()
            : base(typeof(InitializeRelatedRecordOnCreate))
        {
            // not implemented
        }
        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            if (context.MessageName != "Create" || context.Stage != 40)
            {
                throw new InvalidPluginExecutionException("Invalid execution context");
            }
            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
            {
                throw new InvalidPluginExecutionException("Target entity not found in input parameters");
            }
            var aero = new Entity("sua_aeronautical");
            var env = new Entity("sua_environmental");
            Guid aeroId = sysService.Create(aero);
            Guid envId = sysService.Create(env);

            var updateEntity = new Entity(target.LogicalName)
            {
                Id = target.Id,
                ["sua_aeronautical"] = new EntityReference("sua_aeronautical", aeroId),
                ["sua_environmental"] = new EntityReference("sua_environmental", envId)
            };
            sysService.Update(updateEntity);

        }
    }
}
