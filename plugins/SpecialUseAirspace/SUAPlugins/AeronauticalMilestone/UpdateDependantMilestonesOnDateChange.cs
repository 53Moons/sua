using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
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
                            ),
                            new ConditionExpression(
                                "sua_baseline",
                                ConditionOperator.GreaterEqual,
                                baselineDate
                            )
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
                                }
                            }
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

                foreach (var attr in aeroMilestones.Entities.First().Attributes)
                {
                    tracer.Trace($"{attr.Key}: {attr.Value}");
                }

                var aeroMilestoneUpdates = new EntityCollection()
                {
                    EntityName = "sua_aeronauticalmilestone"
                };

                DateTime currentBase = dateCompleted;
                int currentOffset;
                var firstOffsetAlias =
                    aeroMilestones.Entities.First().GetAttributeValue<AliasedValue>("MR.sua_offset")
                    ?? throw new InvalidPluginExecutionException(
                        "Unable to parse new offset for next milestone"
                    );
                try
                {
                    currentOffset = Convert.ToInt32(firstOffsetAlias.Value);
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(
                        "Offset value was not a valid integer",
                        ex
                    );
                }

                foreach (var am in aeroMilestones.Entities)
                {
                    var currentId = am.GetAttributeValue<Guid>("sua_aeronauticalmilestoneid");
                    if (currentId == target.Id)
                        continue;

                    var offsetAlias =
                        am.GetAttributeValue<AliasedValue>("MR.sua_offset")
                        ?? throw new InvalidPluginExecutionException(
                            "Unable to parse new offset for next milestone"
                        );
                    int offsetDays;
                    try
                    {
                        offsetDays = Convert.ToInt32(offsetAlias.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidPluginExecutionException(
                            "Offset value was not a valid integer",
                            ex
                        );
                    }
                    DateTime newBaseline = currentBase.AddDays(offsetDays - currentOffset);
                    var update = new Entity(aeroMilestoneUpdates.EntityName)
                    {
                        Id = am.Id,
                        ["sua_baseline"] = newBaseline,
                    };
                    aeroMilestoneUpdates.Entities.Add(update);

                    currentBase = newBaseline;
                    currentOffset = offsetDays;
                }

                var updateMultipleRequest = new UpdateMultipleRequest
                {
                    Targets = aeroMilestoneUpdates
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
