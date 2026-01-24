
export function RefreshSubgrid(formContext: Xrm.FormContext, subgridName: string): void {
    if (!formContext) {
        console.error("RefreshSubgrid: Form context is required");
        return;
    }
    if (!subgridName || subgridName.trim() === "") {
        console.error("RefreshSubgrid: Subgrid name is required");
        return;
    }

    const subgrid = formContext.getControl(subgridName) as Xrm.Controls.GridControl;
    if (subgrid) {
        subgrid.refresh();
    } else {
        console.error(`RefreshSubgrid: Subgrid with name '${subgridName}' not found`);
    }
}