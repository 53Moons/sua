import { FormNotification } from "./Types";

export function CheckForAttributeValue(
    formContext: Xrm.FormContext,
    attributeName: string
): boolean {
    var hasValue = false;
    if (!formContext) {
        console.error('CheckForAttributeValue: formContext is undefined or null');
        return hasValue;
    }
    if (!attributeName || attributeName.trim() === '') {
        console.error('CheckForAttributeValue: attribute is undefined, null, or empty');
        return hasValue;
    }

    var attribute = formContext.getAttribute(attributeName);
    if (
        attribute &&
        attribute.getValue() !== null &&
        attribute.getValue() !== undefined
    ) {
        hasValue = true;
    }
    return hasValue;
}

export function InitializeNotifications(context: Xrm.Events.EventContext, attributeNames: string[], notification: FormNotification) {
    if (!context) {
        console.error("InitializeNotifications: context is undefined or null");
        return;
    }

    const formContext = context.getFormContext();
    let attributes: Xrm.Attributes.Attribute[] = [];
    for (const attr of attributeNames) {
        const attribute = formContext.getAttribute(attr);
        if (!attribute) {
            console.error(`InitializeNotifications: Attribute not found: ${attr}`);
            continue;
        }
        attributes.push(attribute);
    }
    for (const attribute of attributes) {
        attribute.addOnChange(() => AttributeChangeNotificationHandler(formContext, attributes, notification));
    }
}

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
