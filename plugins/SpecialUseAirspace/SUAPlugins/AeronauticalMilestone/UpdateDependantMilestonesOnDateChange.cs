using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using static SUAPlugins.AeronauticalMilestone.Utilities;
using static SUAPlugins.Utilities;

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

            if (context.MessageName != "Update")
                return;

            var sysService = localPluginContext.SystemUserService;
            var tracer = localPluginContext.TracingService;

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
                throw new InvalidPluginExecutionException("Target not found in context");

            if (!context.PreEntityImages.TryGetValue("PreImage", out Entity preImage))
                throw new InvalidPluginExecutionException("PreImage required on update");

            try
            {
                var aeroRef = GetValueOnUpdate<EntityReference>(
                    target,
                    preImage,
                    "sua_aeronautical"
                );
                if (aeroRef == default)

                    throw new InvalidPluginExecutionException("Aeronautical reference unavailable");

                var dateCompleted = GetValueOnUpdate<DateTime>(
                    target,
                    preImage,
                    "sua_datecompleted"
                );
                if (dateCompleted == default)
                    throw new InvalidPluginExecutionException("Date Completed not found");

                var baselineDate = GetValueOnUpdate<DateTime>(target, preImage, "sua_baseline");
                if (baselineDate == default)
                    throw new InvalidPluginExecutionException("Baseline date not found");

                var aeroMilestoneQuery = new QueryExpression("sua_aeronauticalmilestone")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0), //Active
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                aeroRef.Id
                            ) //,
                            //new ConditionExpression(
                            //    "sua_baseline",
                            //    ConditionOperator.GreaterEqual,
                            //    baselineDate
                            //)
                        }
                    },
                    Orders = { new OrderExpression("sua_baseline", OrderType.Ascending) },
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

                EntityCollection aeroMilestones;
                try
                {
                    aeroMilestones = sysService.RetrieveMultiple(aeroMilestoneQuery);
                }
                catch (Exception ex)
                {
                    var error = "Error fetch related Aeronautical Milestones";
                    tracer.Trace(error, ex.Message);
                    throw new InvalidPluginExecutionException(error, ex);
                }

                tracer.Trace($"Found {aeroMilestones.Entities.Count} related milestones");

                if (aeroMilestones.Entities.Count == 0)
                {
                    return;
                }

                if (!context.PostEntityImages.TryGetValue("PostImage", out Entity postImage))
                    throw new InvalidPluginExecutionException("PostImage required on update");

                var testingCollection = UpdateDependantMilestones(
                    postImage,
                    aeroMilestones,
                    tracer
                );

                var updateMultipleRequest = new UpdateMultipleRequest
                {
                    Targets = testingCollection
                };
                try
                {
                    sysService.Execute(updateMultipleRequest);
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(
                        "Error updating related milestones",
                        ex
                    );
                }

                tracer.Trace("Related milestone baselines updated");
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}
