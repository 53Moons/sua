
import { AttributeChangeNotificationHandler, FormNotification } from "../../General";

export function InitializeAeronauticalNotifications(context: Xrm.Events.EventContext, attributeNames: string[], notification: FormNotification) {
    if (!context) {
        console.error("InitializeAeronauticalNotifications: context is undefined or null");
        return;
    }

    const formContext = context.getFormContext();
    let attributes: Xrm.Attributes.Attribute[] = [];
    for (const attr of attributeNames) {
        const attribute = formContext.getAttribute(attr);
        if (!attribute) {
            console.error(`InitializeAeronauticalNotifications: Attribute not found: ${attr}`);
            continue;
        }
        attributes.push(attribute);
    }
    for (const attribute of attributes) {
        attribute.addOnChange(() => AttributeChangeNotificationHandler(formContext, attributes, notification));
    }
}

