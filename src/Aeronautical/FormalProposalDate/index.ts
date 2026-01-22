export function FormalProposalDateHasValue(formContext: Xrm.FormContext): boolean {
    if (!formContext) {
        console.error("FormalProposalDateHasValue: formContext is undefined or null");
        return false;
    }
    const hasValue = formContext.getAttribute("sua_formalproposaldate")?.getValue() != null;
    console.debug("FormalProposalDateHasValue: hasValue =", hasValue);
    return hasValue;
}