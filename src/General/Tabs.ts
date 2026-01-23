export function ToogleTabVisibility(
    formContext: Xrm.FormContext,
    tabName: string,
    isVisible: boolean
): void {
    if (!formContext) {
        console.error('ToogleTabVisibility: formContext is undefined or null');
        return;
    }
    if (!tabName || tabName.trim() === '') {
        console.error('ToogleTabVisibility: tabName is undefined or null');
        return;
    }

    var tab = formContext.ui.tabs.get(tabName);
    if (!tab) {
        console.error('ToogleTabVisibility: tab is undefined or null');
        return;
    }
    console.debug(`Setting visibility of tab: ${tabName} to ${isVisible}`);
    tab.setVisible(isVisible);
}
