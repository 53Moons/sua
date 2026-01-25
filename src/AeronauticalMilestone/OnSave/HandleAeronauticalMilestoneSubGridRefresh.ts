import { RefreshSubgrid } from "../../General";


export function HandleAeronauticalMilestoneSubGridRefresh(context: Xrm.Events.SaveEventContext) {
    if (!context) {
        console.error("RefreshSubgridOnSave: context is undefined or null");
        return;
    }
    // Retrieved from global scope and set by Aeronautical/OnLoad/ReloadMilestoneSubgrid
    const formContext = (window.top as any).__aeronauticalFormContext as Xrm.FormContext | undefined;
    if (!formContext) {
        console.error("Parent formContext not found. Ensure OnLoad handler ran first.");
        return;
    }

    RefreshSubgrid(formContext, "Milestones_Subgrid")
}

