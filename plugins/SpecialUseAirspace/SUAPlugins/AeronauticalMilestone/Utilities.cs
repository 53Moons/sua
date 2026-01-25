using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using static SUAPlugins.Utilities;

namespace SUAPlugins.AeronauticalMilestone
{
    public static class Utilities
    {
        public static void RemoveExistingAeronauticalMilestones(
            EntityReference aeronauticalRef,
            IOrganizationService service
        )
        {
            if (aeronauticalRef == null)
                throw new InvalidPluginExecutionException("Aeronautical reference is null");
            if (service == null)
                throw new InvalidPluginExecutionException("IOrganizationService is null");
            try
            {
                var query = new QueryExpression("sua_aeronauticalmilestone")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                aeronauticalRef.Id
                            )
                        }
                    }
                };
                var existingMilestones = service.RetrieveMultiple(query).Entities;
                if (existingMilestones.Count == 0)
                    return;

                foreach (var milestone in existingMilestones)
                {
                    service.Delete(milestone.LogicalName, milestone.Id);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Error removing existing aeronautical milestones: " + ex.Message
                );
            }
        }

        public static EntityCollection GetRelevantMilestones(
            Entity action,
            Entity aero,
            Entity preAero,
            IOrganizationService service,
            ITracingService tracer,
            bool supplementalAugmentOnly = false
        )
        {
            EntityCollection milestones;
            try
            {
                if (!action.TryGetAttributeValue("sua_rulemaking", out bool isRulemaking))
                {
                    throw new InvalidPluginExecutionException("Rule Making not found on action");
                }
                tracer.Trace($"IsRulemaking: {isRulemaking}");

                if (!action.TryGetAttributeValue("sua_temporary", out bool isTemporary))
                {
                    throw new InvalidPluginExecutionException("Temporary not found on Action");
                }
                tracer.Trace($"IsTemporary: {isTemporary}");

                var requiresSupplemental = GetValueOnUpdate<bool>(
                    aero,
                    preAero,
                    "sua_requiressupplementalrulemaking"
                );
                tracer.Trace($"Requires Supplemental Rulemaking {requiresSupplemental}");

                var typeOfAction = GetValueOnUpdate<OptionSetValue>(
                    aero,
                    preAero,
                    "sua_typeofaction"
                );

                var milestoneQuery = new QueryExpression("sua_milestone")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
                            new ConditionExpression(
                                "sua_applicability",
                                ConditionOperator.In,
                                2,
                                isTemporary ? 0 : 1
                            ),
                            new ConditionExpression(
                                "sua_rulemakingtype",
                                ConditionOperator.In,
                                2,
                                isRulemaking ? 0 : 1
                            ),
                            new ConditionExpression(
                                "sua_appliesto",
                                ConditionOperator.ContainValues,
                                typeOfAction.Value
                            )
                        }
                    },
                    Orders = { new OrderExpression("sua_activeoffset", OrderType.Ascending) }
                };
                if (supplementalAugmentOnly)
                {
                    milestoneQuery.Criteria.AddCondition(
                        new ConditionExpression(
                            "sua_supplementaltype",
                            ConditionOperator.Equal,
                            requiresSupplemental ? 0 : 1
                        )
                    );
                }
                else
                {
                    milestoneQuery.Criteria.AddCondition(
                        new ConditionExpression(
                            "sua_supplementaltype",
                            ConditionOperator.In,
                            2,
                            requiresSupplemental ? 0 : 1
                        )
                    );
                }

                try
                {
                    milestones = service.RetrieveMultiple(milestoneQuery);
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }

                tracer.Trace($"Found {milestones.Entities.Count} applicable milestones");
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }

            return milestones;
        }

        public static void InitializeMilestones(
            Entity aero,
            Entity preAero,
            IOrganizationService service,
            ITracingService tracer
        )
        {
            try
            {
                RemoveExistingAeronauticalMilestones(aero.ToEntityReference(), service);
                var formalProposalDate = GetValueOnUpdate<DateTime>(
                    aero,
                    preAero,
                    "sua_formalproposaldate"
                );
                if (formalProposalDate == default)
                {
                    tracer.Trace("Formal Proposal Date not found, skipping milestone creation");
                    return;
                }
                tracer.Trace($"Formal Proposal Date: {formalProposalDate}");

                var actionQuery = new QueryExpression("sua_action")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "sua_aeronautical",
                                ConditionOperator.Equal,
                                aero.Id
                            )
                        }
                    }
                };

                Entity action;
                try
                {
                    action =
                        service.RetrieveMultiple(actionQuery).Entities.FirstOrDefault()
                        ?? throw new InvalidPluginExecutionException("Related Action not found");
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }

                tracer.Trace($"Related Action: {action.Id}");

                EntityCollection milestones = GetRelevantMilestones(
                    action,
                    aero,
                    preAero,
                    service,
                    tracer
                );

                tracer.Trace($"Found {milestones.Entities.Count} applicable milestones");

                if (milestones.Entities.Count == 0)
                {
                    return;
                }

                var aeronauticalMilestones = new EntityCollection()
                {
                    EntityName = "sua_aeronauticalmilestone"
                };

                foreach (var milestone in milestones.Entities)
                {
                    var relatedMilestone = new Entity(aeronauticalMilestones.EntityName)
                    {
                        ["sua_milestone"] = milestone.ToEntityReference(),
                        ["sua_aeronautical"] = aero.ToEntityReference(),
                        ["sua_baseline"] = formalProposalDate.AddDays(
                            milestone.GetAttributeValue<int>("sua_activeoffset")
                        )
                    };
                    aeronauticalMilestones.Entities.Add(relatedMilestone);
                }

                var createRequest = new CreateMultipleRequest()
                {
                    Targets = aeronauticalMilestones
                };

                try
                {
                    service.Execute(createRequest);
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }

                tracer.Trace("Initial Milestones related");
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message, ex);
            }
        }

        public static EntityCollection UpdateDependantMilestones(
            Entity aeroMilestone,
            EntityCollection aeroMilestones,
            ITracingService tracer
        )
        {
            if (aeroMilestone == null)
                throw new ArgumentNullException(nameof(aeroMilestone));
            if (aeroMilestones == null)
                throw new ArgumentNullException(nameof(aeroMilestones));

            tracer.Trace($"Updating dependant milestones for trigger milestone {aeroMilestone.Id}");

            if (!aeroMilestone.TryGetAttributeValue("sua_baseline", out DateTime triggerBaseline))
                throw new InvalidPluginExecutionException(
                    "Baseline date not found on triggering milestone."
                );

            bool triggerHasCompleted = aeroMilestone.TryGetAttributeValue(
                "sua_datecompleted",
                out DateTime dateCompleted
            );

            tracer.Trace(
                $"Trigger Baseline: {triggerBaseline:d}, Completed: {triggerHasCompleted} "
                    + (triggerHasCompleted ? $", Date Completed: {dateCompleted:d}" : "")
            );

            // Anchor date: if completed, downstream starts from completion; otherwise baseline.
            DateTime triggerAnchor = triggerHasCompleted ? dateCompleted : triggerBaseline;

            // If trigger is completed, it contributes NO delay downstream.
            int prevDelay = triggerHasCompleted
                ? 0
                : (aeroMilestone.GetAttributeValue<int?>("sua_anticipateddelay") ?? 0);

            // Need trigger offset to compute deltas.
            var aeroMilestoneWithOffset =
                aeroMilestones.Entities.FirstOrDefault(m => m.Id == aeroMilestone.Id)
                ?? throw new Exception(
                    "Triggering milestone required in collection of related milestones."
                );
            int prevOffset = GetAliasedInt(aeroMilestoneWithOffset, "M.sua_activeoffset");

            var ordered = aeroMilestones.Entities
                .Where(m => m.Id != aeroMilestone.Id)
                .Where(m => m.Contains("sua_baseline"))
                .OrderBy(m => m.GetAttributeValue<DateTime>("sua_baseline"))
                .ToList();

            // Keep your original intent: only recalc milestones at/after the trigger baseline.
            ordered = ordered
                .Where(
                    m =>
                        m.GetAttributeValue<DateTime>("sua_baseline") >= triggerBaseline
                        && m.GetAttributeValue<DateTime>("sua_datecompleted") == default
                )
                .ToList();

            tracer.Trace($"Found {ordered.Count} dependant milestones to update after filtering");

            var updates = new EntityCollection { EntityName = "sua_aeronauticalmilestone" };

            DateTime prevOriginalBaseline = triggerBaseline; // your clamp reference
            DateTime currentAnchor = triggerAnchor;

            foreach (var m in ordered)
            {
                int currentOffset = GetAliasedInt(m, "M.sua_activeoffset");

                // Baseline for THIS milestone = previous anchor + (offset delta) + (previous milestone's delay)
                int delta = currentOffset - prevOffset;
                DateTime candidate = currentAnchor.AddDays(delta + prevDelay);

                // Your clamp: don't move earlier than previous "stored" baseline
                DateTime applicable =
                    candidate < prevOriginalBaseline ? prevOriginalBaseline : candidate;

                tracer.Trace(
                    $"Updating {m.GetAttributeValue<string>("sua_name")} "
                        + $"from {m.GetAttributeValue<DateTime>("sua_baseline"):d} "
                        + $"to {applicable:d} "
                        + $"(delta={delta}, prevDelay={prevDelay})"
                );

                updates.Entities.Add(
                    new Entity(updates.EntityName) { Id = m.Id, ["sua_baseline"] = applicable }
                );

                // Move forward in the chain:
                currentAnchor = applicable;
                prevOffset = currentOffset;

                tracer.Trace(
                    $"{m.GetAttributeValue<string>("sua_name")} hasDelayAttr={m.Attributes.Contains("sua_anticipateddelay")} "
                        + $"delayValue={(m.GetAttributeValue<int?>("sua_anticipateddelay")?.ToString() ?? "null")}"
                );

                // IMPORTANT: this milestone's delay affects the NEXT milestone (even if THIS milestone is completed)
                prevDelay = m.GetAttributeValue<int?>("sua_anticipateddelay") ?? 0;

                // Keep your original baseline clamp behavior
                prevOriginalBaseline = m.GetAttributeValue<DateTime>("sua_baseline");
            }

            return updates;
        }

        public static EntityCollection RebaseRelatedMilestones(
            Entity aeroMilestone, // POST IMAGE (final state)
            EntityCollection aeroMilestones, // ALL related milestones (includes trigger)
            ITracingService tracer
        )
        {
            if (aeroMilestone == null)
                throw new ArgumentNullException(nameof(aeroMilestone));
            if (aeroMilestones == null)
                throw new ArgumentNullException(nameof(aeroMilestones));

            if (!aeroMilestone.TryGetAttributeValue("sua_baseline", out DateTime triggerBaseline))
                throw new InvalidPluginExecutionException(
                    "Trigger milestone missing sua_baseline in post image."
                );

            bool triggerHasCompleted = aeroMilestone.TryGetAttributeValue(
                "sua_datecompleted",
                out DateTime triggerCompletedDate
            );

            // Anchor: completed => DateCompleted, else => baseline
            DateTime currentAnchor = triggerHasCompleted ? triggerCompletedDate : triggerBaseline;

            // Critical rule: if completed, delay does NOT carry downstream (even if field has a value)
            int prevDelay = triggerHasCompleted
                ? 0
                : (aeroMilestone.GetAttributeValue<int?>("sua_anticipateddelay") ?? 0);

            // Need the trigger's offset from the retrieved collection (aliased value)
            var triggerInCollection =
                aeroMilestones.Entities.FirstOrDefault(e => e.Id == aeroMilestone.Id)
                ?? throw new InvalidPluginExecutionException(
                    "Trigger milestone must be included in aeroMilestones collection."
                );

            int triggerOffset = GetAliasedInt(triggerInCollection, "M.sua_activeoffset");
            int prevOffset = triggerOffset;

            tracer.Trace(
                $"Trigger={aeroMilestone.GetAttributeValue<string>("sua_name")} "
                    + $"baseline={triggerBaseline:d} completed={triggerHasCompleted} "
                    + $"{(triggerHasCompleted ? $"dateCompleted={triggerCompletedDate:d} " : "")}"
                    + $"triggerOffset={triggerOffset} triggerDelayField={(aeroMilestone.GetAttributeValue<int?>("sua_anticipateddelay") ?? 0)} prevDelayApplied={prevDelay}"
            );

            // Walk downstream by OFFSET (not baseline) to avoid negative deltas / unstable ordering
            var chain = aeroMilestones.Entities
                .Where(m => m.Id != aeroMilestone.Id)
                .Select(m => new { Entity = m, Offset = TryGetAliasedInt(m, "M.sua_activeoffset") })
                .Where(x => x.Offset.HasValue && x.Offset.Value > triggerOffset)
                .OrderBy(x => x.Offset.Value)
                .Select(x => x.Entity)
                .ToList();

            tracer.Trace($"Found {chain.Count} downstream milestones by offset.");

            var updates = new EntityCollection { EntityName = "sua_aeronauticalmilestone" };

            foreach (var m in chain)
            {
                int currentOffset = GetAliasedInt(m, "M.sua_activeoffset");
                int delta = currentOffset - prevOffset;

                bool mHasCompleted = m.TryGetAttributeValue(
                    "sua_datecompleted",
                    out DateTime mCompletedDate
                );

                if (mHasCompleted)
                {
                    // Completed milestones:
                    // - do not recalc their baseline
                    // - anchor becomes their completion date (ground truth)
                    // - delay does not carry past completion (even if delay field is non-zero)
                    tracer.Trace(
                        $"Encountered completed milestone {m.GetAttributeValue<string>("sua_name")} "
                            + $"offset={currentOffset} dateCompleted={mCompletedDate:d}. "
                            + $"Resetting anchor and clearing carried delay."
                    );

                    currentAnchor = mCompletedDate;
                    prevDelay = 0;
                    prevOffset = currentOffset;
                    continue;
                }

                DateTime newBaseline = currentAnchor.AddDays(delta + prevDelay);

                tracer.Trace(
                    $"Updating {m.GetAttributeValue<string>("sua_name")} "
                        + $"offset={currentOffset} delta={delta} prevDelay={prevDelay} "
                        + $"oldBaseline={m.GetAttributeValue<DateTime>("sua_baseline"):d} newBaseline={newBaseline:d} "
                        + $"delayField={(m.GetAttributeValue<int?>("sua_anticipateddelay") ?? 0)}"
                );

                updates.Entities.Add(
                    new Entity(updates.EntityName) { Id = m.Id, ["sua_baseline"] = newBaseline }
                );

                // Advance the chain
                currentAnchor = newBaseline;
                prevOffset = currentOffset;

                // Carry THIS milestone's delay to the NEXT milestone (since this milestone isn't completed)
                prevDelay = m.GetAttributeValue<int?>("sua_anticipateddelay") ?? 0;
            }

            return updates;
        }

        public static void UpdateLatestMilestoneBaseline(
            EntityReference aeroRef,
            IOrganizationService service
        )
        {
            if (aeroRef == null)
                throw new ArgumentNullException(nameof(aeroRef));
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var aeroMilestoneQuery = new QueryExpression("sua_aeronauticalmilestone")
            {
                ColumnSet = new ColumnSet("sua_baseline", "sua_datecompleted"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression(
                            "sua_aeronautical",
                            ConditionOperator.Equal,
                            aeroRef.Id
                        ),
                        new ConditionExpression("sua_datecompleted", ConditionOperator.Null)
                    }
                },
                Orders = { new OrderExpression("sua_baseline", OrderType.Ascending) }
                //,
                //LinkEntities =
                //{
                //    new LinkEntity
                //    {
                //        LinkFromEntityName = "sua_aeronauticalmilestone",
                //        LinkFromAttributeName = "sua_milestone",
                //        LinkToEntityName = "sua_milestone",
                //        LinkToAttributeName = "sua_milestoneid",
                //        Columns = new ColumnSet("sua_name", "sua_activeoffset"),
                //        EntityAlias = "M",
                //        Orders = { new OrderExpression("sua_activeoffset", OrderType.Ascending) }
                //    }
                //}
            };

            var aeroMilestones = service.RetrieveMultiple(aeroMilestoneQuery).Entities;
            if (aeroMilestones.Count == 0)
                return;

            var lastMilestone = aeroMilestones
                .OrderBy(m => m.GetAttributeValue<DateTime>("sua_baseline"))
                .Last();

            var aeroEntity = new Entity("sua_aeronautical") { Id = aeroRef.Id };
            aeroEntity["sua_lastmilestonebaseline"] = lastMilestone.GetAttributeValue<DateTime>(
                "sua_baseline"
            );

            service.Update(aeroEntity);
        }
    }
}
