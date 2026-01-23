using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SUAPlugins.Action
{
    public static class Utilities
    {
        public static Entity GetActionFromAeronauticalRef(
            EntityReference aeroRef,
            IOrganizationService service,
            ITracingService tracer
        )
        {
            if (aeroRef == null)
                throw new ArgumentNullException("aeroRef");
            if (service == null)
                throw new ArgumentNullException("service");
            if (tracer == null)
                throw new ArgumentNullException("tracer");

            Entity action;
            try
            {
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
                                aeroRef.Id
                            )
                        }
                    }
                };

                action =
                    service.RetrieveMultiple(actionQuery).Entities.FirstOrDefault()
                    ?? throw new InvalidPluginExecutionException("Related Action not found");

                tracer.Trace($"Related Action: {action.Id}");
            }
            catch (Exception ex)
            {
                tracer.Trace($"Error retrieving Action from Aeronautical: {ex.Message}");
                throw;
            }

            return action;
        }
    }
}
