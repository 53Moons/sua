import { FormNotification } from "../../General";

export function NotifyOnRequiresSupplementalChange(context: Xrm.Events.EventContext): void {
    if (!context) {
        console.error("NotifyOnRequiresSupplementalChange: context is undefined or null");
        return;
    }

    const formContext = context.getFormContext();

    const notification: FormNotification = {
        message: "Requires Supplemental Rulemaking has changed.",
        level: "WARNING",
        id: "RequiresSupplementalRulemakingChanged"
    }

    const requiresSupplementalRulemakingAttribute = formContext.getAttribute("sua_requiressupplementalrulemaking");
    if (!requiresSupplementalRulemakingAttribute) {
        console.error("NotifyOnRequiresSupplementalChange: Attribute 'sua_requiressupplementalrulemaking' not found");
        return;
    }
    if (!requiresSupplementalRulemakingAttribute.getIsDirty()) {
        console.debug("NotifyOnRequiresSupplementalChange: 'sua_requiressupplementalrulemaking' attribute has not changed");
        formContext.ui.clearFormNotification(notification.id);
        return;
    }
    const newValue = requiresSupplementalRulemakingAttribute.getValue();
    notification.message = newValue == 0 ?
        "Changing to not require supplemental rulemaking. This change will remove any milestones related to supplemental rulemaking and add milestones that do not."
        : "Changing to require supplemental rulemaking. This change will add milestones related to supplemental rulemaking and remove milestones that do not.";

    notification.message = notification.message + " This action cannot be undone.";
    console.debug("NotifyOnRequiresSupplementalChange: 'sua_requiressupplementalrulemaking' attribute has changed");
    formContext.ui.setFormNotification(notification.message, notification.level, notification.id);
} 