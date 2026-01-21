export function CheckForAttributeValue(formContext: Xrm.FormContext, attributeName: string): boolean {
    var hasValue = false;
    if (!formContext) {
        console.error("CheckForAttributeValue: formContext is undefined or null");
        return hasValue;
    }
    if (!attributeName || attributeName.trim() === "") {
        console.error("CheckForAttributeValue: attribute is undefined, null, or empty");
        return hasValue;
    }

    var attribute = formContext.getAttribute(attributeName);
    if (attribute && attribute.getValue() !== null && attribute.getValue() !== undefined) {
        hasValue = true;
    }
    return hasValue;
}