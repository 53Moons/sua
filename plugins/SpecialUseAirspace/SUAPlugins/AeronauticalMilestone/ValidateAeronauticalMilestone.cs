using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using static SUAPlugins.Utilities;

namespace SUAPlugins.AeronauticalMilestone
{
    public class ValidateAeronauticalMilestone : PluginBase
    {
        public ValidateAeronauticalMilestone()
            : base(typeof(ValidateAeronauticalMilestone))
        {
            // not implemented
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;

            if (
                (context.MessageName != "Create" && context.MessageName != "Update")
                || context.PrimaryEntityName != "sua_aeronauticalmilestone"
                || context.Stage != 10
            )
            {
                throw new InvalidPluginExecutionException(
                    "Invalid registration for ValidateAeronauticalMilestone plugin."
                );
            }

            if (!context.InputParameters.TryGetValue("Target", out Entity target))
            {
                throw new InvalidPluginExecutionException(
                    "Target entity is missing in the plugin context."
                );
            }

            if (
                !context.PreEntityImages.TryGetValue("PreImage", out Entity preImage)
                && context.MessageName == "Update"
            )
            {
                throw new InvalidPluginExecutionException(
                    "PreImage is required for Update operations in ValidateAeronauticalMilestone plugin."
                );
            }
            ValidateCompletedAndDelay(target, preImage);
            if (
                context.MessageName == "Update"
                || target.Attributes.ContainsKey("sua_datecompleted")
            )
            {
                ValidateDateCompletedValue(
                    target,
                    preImage,
                    localPluginContext.CurrentUserService,
                    localPluginContext.TracingService
                );
                ValidateDateCompletedOrder(target, preImage, localPluginContext.CurrentUserService);
            }
        }

        private const string DateCompleted = "sua_datecompleted";
        private const string AnticipatedDelay = "sua_anticipateddelay";

        private static void ValidateCompletedAndDelay(Entity target, Entity preImage)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            // Treat missing preImage on Create as "all null"
            DateTime? preCompleted = preImage?.GetAttributeValue<DateTime?>(DateCompleted);
            int? preDelay = GetNullableInt(preImage, AnticipatedDelay);

            // Target may or may not include the attribute.
            // "New" values are what the record will look like after this operation:
            // - If Target contains attribute => use it (even if null)
            // - Else fall back to PreImage
            bool targetHasCompleted = target.Attributes.ContainsKey(DateCompleted);
            bool targetHasDelay = target.Attributes.ContainsKey(AnticipatedDelay);

            DateTime? newCompleted = targetHasCompleted
                ? target.GetAttributeValue<DateTime?>(DateCompleted)
                : preCompleted;

            int? newDelay = targetHasDelay ? GetNullableInt(target, AnticipatedDelay) : preDelay;

            // Detect whether the caller actually changed these fields in this operation.
            bool completedChanged =
                targetHasCompleted && !NullableDateTimeEqual(preCompleted, newCompleted);
            bool delayChanged = targetHasDelay && preDelay != newDelay;

            // -------------------
            // Rule 1:
            // If the completed date has changed from a date to another, throw an error.
            // (i.e., pre had a value, and new has a value, and they're different)
            // -------------------
            if (completedChanged && preCompleted.HasValue && newCompleted.HasValue)
            {
                throw new InvalidPluginExecutionException(
                    "Completed Date cannot be changed once it has been set."
                );
            }

            // -------------------
            // Rule 2:
            // If completed date changed from null -> date AND delay changed from null -> not null, throw.
            // (i.e., they tried to set both at once from blank state)
            // -------------------
            bool completedNullToDate =
                completedChanged && !preCompleted.HasValue && newCompleted.HasValue;
            bool delayNullToValue = delayChanged && !preDelay.HasValue && newDelay.HasValue;

            if (completedNullToDate && delayNullToValue)
            {
                throw new InvalidPluginExecutionException(
                    "You cannot set Completed Date and Anticipated Delay at the same time when both were previously blank."
                );
            }

            // -------------------
            // Rule 3:
            // If delay changed in any way AND completed date wasn't null in pre-image, throw.
            // (i.e., once completed is set, delay is locked)
            // -------------------
            if (delayChanged && preCompleted.HasValue)
            {
                throw new InvalidPluginExecutionException(
                    "Anticipated Delay cannot be changed after Completed Date has been set."
                );
            }
        }

        private static int? GetNullableInt(Entity e, string attributeName)
        {
            if (e == null)
                return null;

            if (!e.Attributes.TryGetValue(attributeName, out var value) || value == null)
                return null;

            // Dataverse whole number columns come back as int
            return value as int? ?? (value is int i ? i : (int?)null);
        }

        private static bool NullableDateTimeEqual(DateTime? a, DateTime? b) =>
            a.HasValue == b.HasValue && (!a.HasValue || a.Value.Equals(b.Value));

        private static void ValidateDateCompletedValue(
            Entity target,
            Entity preImage,
            IOrganizationService service,
            ITracingService tracer
        )
        {
            if (!target.TryGetAttributeValue("sua_datecompleted", out DateTime completionDate))
                return;

            var baselineDate = GetValueOnUpdate<DateTime>(target, preImage, "sua_baseline");
            if (baselineDate == DateTime.MinValue)
                throw new InvalidPluginExecutionException("Baseline date is required.");

            var aeroRef =
                GetValueOnUpdate<EntityReference>(target, preImage, "sua_aeronautical")
                ?? throw new InvalidPluginExecutionException("Aeronautical reference is required.");
            var aeroMilestoneQuery = new QueryExpression("sua_aeronauticalmilestone")
            {
                ColumnSet = new ColumnSet("sua_baseline", "sua_datecompleted"),
                Orders = { new OrderExpression("sua_baseline", OrderType.Ascending) },
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression(
                            "sua_baseline",
                            ConditionOperator.LessEqual,
                            baselineDate
                        ),
                        new ConditionExpression("sua_datecompleted", ConditionOperator.NotNull),
                        new ConditionExpression(
                            "sua_aeronautical",
                            ConditionOperator.Equal,
                            aeroRef.Id
                        )
                    }
                }
            };
            var aeroMilestoneRecords = service.RetrieveMultiple(aeroMilestoneQuery);
            if (aeroMilestoneRecords.Entities.Count == 0)
                return;
            var latestMilestone = aeroMilestoneRecords.Entities[
                aeroMilestoneRecords.Entities.Count - 1
            ];
            var latestCompletionDate = latestMilestone.GetAttributeValue<DateTime>(
                "sua_datecompleted"
            );
            tracer.Trace(
                $"Latest completed milestone date: {latestCompletionDate}, New completion date: {completionDate}"
            );
            if (completionDate < latestCompletionDate)
            {
                throw new InvalidPluginExecutionException(
                    "Completion Date cannot be earlier than the latest completed milestone's Completion Date."
                );
            }
        }

        public static void ValidateDateCompletedOrder(
            Entity target,
            Entity preImage,
            IOrganizationService service
        )
        {
            if (!target.TryGetAttributeValue("sua_datecompleted", out DateTime _))
                return;

            if (!target.TryGetAttributeValue("sua_activeoffset", out decimal activeOffset))
            {
                if (!preImage.TryGetAttributeValue("sua_activeoffset", out activeOffset))
                {
                    throw new InvalidPluginExecutionException(
                        "Active Offset (sua_activeoffset) not found on Target or PreImage."
                    );
                }
            }

            if (
                !target.TryGetAttributeValue(
                    "sua_aeronautical",
                    out EntityReference aeronauticalRef
                )
            )
            {
                if (!preImage.TryGetAttributeValue("sua_aeronautical", out aeronauticalRef))
                {
                    throw new InvalidPluginExecutionException(
                        "Aeronautical reference (sua_aeronautical) not found on Target or PreImage."
                    );
                }
            }

            var aeroMilestoneQuery = new QueryExpression("sua_aeronauticalmilestone")
            {
                ColumnSet = new ColumnSet("sua_aeronauticalmilestoneid", "sua_datecompleted"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression(
                            "sua_aeronauticalmilestoneid",
                            ConditionOperator.NotEqual,
                            target.Id
                        ),
                        new ConditionExpression(
                            "sua_aeronautical",
                            ConditionOperator.Equal,
                            aeronauticalRef.Id
                        ),
                        new ConditionExpression(
                            "sua_activeoffset",
                            ConditionOperator.LessEqual,
                            activeOffset
                        ),
                        new ConditionExpression("sua_datecompleted", ConditionOperator.Null)
                    }
                }
            };

            var milestones = service.RetrieveMultiple(aeroMilestoneQuery);
            if (milestones.Entities.Count > 0)
            {
                throw new InvalidPluginExecutionException(
                    "Cannot complete this milestone while prior milestones remain incomplete."
                );
            }
        }
    }
}
