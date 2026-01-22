

export function TypeOfActionHasValue(formContext: Xrm.FormContext): boolean {
    if (!formContext) {
        console.error("TypeOfActionHasValue: formContext is undefined or null");
        return false;
    }
    var typeOfAction = formContext.getControl("sua_typeofaction") as Xrm.Controls.OptionSetControl;
    if (!typeOfAction) {
        console.error("handleSectionVisibility: typeOfAction control is not found on the form");
        return false;
    }
    const typeOfActionAttribute = typeOfAction.getAttribute();
    if (!typeOfActionAttribute) {
        console.error("TypeOfActionHasValue: typeOfAction attribute is not found on the form");
        return false;
    }
    const typeOfActionValue = typeOfActionAttribute.getValue();

    var hasValue = typeOfActionValue != null;

    console.debug("TypeOfActionHasValue: hasValue =", hasValue, typeOfActionValue);
    return hasValue;
}