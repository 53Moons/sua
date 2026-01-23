import { FormNotification } from "./Aeronautical/OnLoad";


export function AttributeChangeNotificationHandler(
    formContext: Xrm.FormContext,
    attributes: Xrm.Attributes.Attribute[],
    notification: FormNotification
): void {
    if (!formContext) {
        console.error('EvaluateResetMilestoneWarning: formContext is undefined or null');
        return;
    }
    if (!attributes || attributes.length === 0) {
        console.error('EvaluateResetMilestoneWarning: attributes array is undefined, null, or empty');
        return;
    }
    if (!notification) {
        console.error('EvaluateResetMilestoneWarning: notification is undefined or null');
        return;
    }

    let hasChanged = false;
    for (const attribute of attributes) {
        if (!attribute) {
            console.error(
                `EvaluateResetMilestoneWarning: Attribute not found: ${attribute}`
            );
            continue;
        }

        hasChanged = attribute.getIsDirty();
        console.debug(
            `EvaluateResetMilestoneWarning: Attribute ${attribute.getName()} changed: ${hasChanged}`
        );
        if (hasChanged) {
            break;
        }
    }
    if (hasChanged) {
        formContext.ui.setFormNotification(
            notification.message,
            notification.level,
            notification.id
        );
    } else {
        formContext.ui.clearFormNotification(notification.id);
    }
}

