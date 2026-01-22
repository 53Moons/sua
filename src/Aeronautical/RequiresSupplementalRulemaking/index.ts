export function RequiresSupplementalRulemakingHasValue(formContext: Xrm.FormContext): boolean {
    if (!formContext) {
        console.error("RequiresSupplementalRulemakingHasValue: formContext is undefined or null");
        return false;
    }
    const attribute = formContext.getAttribute("sua_requiressupplementalrulemaking");
    if (!attribute) {
        console.error("RequiresSupplementalRulemakingHasValue: attribute sua_requiressupplementalrulemaking is not found on the form");
        return false;
    }
    const hasValue = attribute?.getValue() != null;
    console.debug("RequiresSupplementalRulemakingHasValue: hasValue =", hasValue);
    return hasValue;
}