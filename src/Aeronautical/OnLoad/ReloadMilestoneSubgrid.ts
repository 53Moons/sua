import { RefreshSubgrid } from "../../General";


export function AddReloadMilestoneSubgridHandler(context: Xrm.Events.EventContext, subgridName: string = "Milestones_Subgrid"): void {
    if (!context) {
        console.error("ReloadMilestoneSubgrid: Save event context is required");
        return;
    }
    const formContext = context.getFormContext();
    // Stores the formContext in the top window object for access by other scripts
    (window.top as any).__aeronauticalFormContext = formContext;
    formContext.data.entity.addOnPostSave(() => {
        try {
            RefreshSubgrid(formContext, subgridName);
            console.debug(`ReloadMilestoneSubgrid: Subgrid ${subgridName} refreshed`);
        }
        catch (error) {
            console.error("ReloadMilestoneSubgrid: Error refreshing subgrid", error);
        }
    });
}