export * from './TypeOfAction'
export * from './RequiresSupplementalRulemaking'
export * from './FormalProposalDate'

import { FormalProposalDateHasValue } from "./FormalProposalDate";
import { RequiresSupplementalRulemakingHasValue } from "./RequiresSupplementalRulemaking";
import { TypeOfActionHasValue } from './TypeOfAction';


export function HandleAeronauticalVisibility(context: Xrm.Events.EventContext) {
    if (!context) {
        console.error("HandleAeronauticalVisibility: context is undefined or null");
        return;
    }

    const formContext = context.getFormContext();
    const generalTab = formContext.ui.tabs.get("General");
    if (!generalTab) {
        console.error("HandleAeronauticalVisibility: General tab is not found on the form");
        return;
    }

    const typeOfActionHasValue = TypeOfActionHasValue(formContext);

    // Phase Section
    const phaseSection = generalTab.sections.get("Phase_Section");
    if (!phaseSection) {
        console.error("HandleAeronauticalVisibility: Phase_Section is not found on the General tab");
    }
    else {
        phaseSection.setVisible(typeOfActionHasValue);
    }

    // Dates Section
    const datesSection = generalTab.sections.get("Dates_Section");
    if (!datesSection) {
        console.error("HandleAeronauticalVisibility: Dates_Section is not found on the General tab");
    }
    else {
        datesSection.setVisible(typeOfActionHasValue);
        if (typeOfActionHasValue) {
            HandleMilestoneSubgridVisibility(formContext);
        }

    }
}

function HandleMilestoneSubgridVisibility(formContext: Xrm.FormContext) {
    if (!formContext) {
        console.error("HandleMilestoneSubgridVisibility: formContext is undefined or null");
        return;
    }

    var milestoneSubgrid = formContext.getControl("Milestones_Subgrid") as Xrm.Controls.GridControl;
    if (!milestoneSubgrid) {
        console.error("HandleMilestoneSubgridVisibility: Milestones_Subgrid control is not found on the form");
        return;
    }
    var hasValue = FormalProposalDateHasValue(formContext) && RequiresSupplementalRulemakingHasValue(formContext);

    console.debug("HandleMilestoneSubgridVisibility: hasValue =", hasValue);
    milestoneSubgrid.setVisible(hasValue);
}
