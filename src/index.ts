export * from './Action'
export * from './Aeronautical'
export * from './AttributeChangeNotificationHandler'

let globalNotifications: string[] = [];

export function ShowMilestoneResetWarning(context: Xrm.Events.EventContext, attributes: string[]) {
    if (!context) {
        console.error("ShowMilestoneResetWarning: context is undefined or null");
        return;
    }

    const formContext = context.getFormContext();

    if (!formContext.data.getIsDirty()) {
        for (let notification of globalNotifications) {
            Xrm.App.clearGlobalNotification(notification).then((result) => {
                console.debug("Global notifications cleared because the form is not dirty.");
                return;
            })
        }
    }

    function triggerNotificationOnChange() {
        const action: Xrm.App.Action = {
            actionLabel: "Clear changes", eventHandler: function () {
                formContext.data.refresh(false)
            }
        }
        const notfication: Xrm.App.Notification = {
            level: 3,
            type: 2,
            message: "The current changes on the form will reset all milestones. This action cannot be undone.",
            action
        }

        Xrm.App.addGlobalNotification(notfication).then(
            (id) => {

                if (globalNotifications.length == 0) {
                    globalNotifications.push(id);
                }
                else {
                    Xrm.App.clearGlobalNotification(globalNotifications[0])
                }
            }
        );
    }

    for (let attributeName of attributes) {
        const attribute = formContext.getAttribute(attributeName);
        if (!attribute) {
            console.error(`ShowMilestoneResetWarning: Attribute not found: ${attributeName}`);
            return;
        }
        attribute.addOnChange(triggerNotificationOnChange)
    }


}