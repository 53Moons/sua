export * from './Attributes';
export * from './Tabs';
export * from './Types';
export * from './Subgrid';

export function PreventAutoSave(context: Xrm.Events.SaveEventContext): void {
    if (!context) return;
    const eventArgs = context.getEventArgs();
    const saveMode = eventArgs.getSaveMode();
    if (saveMode == 70) {
        eventArgs.preventDefault();
        console.debug('PreventAutoSave: Save operation prevented for saveMode 70 (AutoSave)');
    }
}

export function ClearMilestoneWarningNotificationsPostSave(context: Xrm.Events.SaveEventContext): void {
    if (!context) return;
    const formContext = context.getFormContext();
    formContext.ui.clearFormNotification('MILESTONE_RESET_WARNING');
}
