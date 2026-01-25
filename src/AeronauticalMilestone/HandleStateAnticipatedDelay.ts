
export function HandleStateAnticipatedDelayOnLoad(context: Xrm.Events.EventContext): void {
    if (!context) {
        console.error("HandleStateAnticipatedDelay: context is undefined or null");
        return;
    }

    const formContext = context.getFormContext();

    const dateCompleted = formContext.getAttribute("sua_datecompleted");
    const anticipatedDelay = formContext.getAttribute("sua_anticipateddelay");

    HandleStateDateCompletedAnticipatedDelay(dateCompleted, anticipatedDelay);
}

export function HandleStateDateCompletedAnticipatedDelay(dateCompleted: Xrm.Attributes.Attribute, anticipatedDelay: Xrm.Attributes.Attribute): void {
    if (!dateCompleted || !anticipatedDelay) {
        console.warn("HandleStateDateCompletedAnticipatedDelay: One or all attributes are undefined or null");
        return;
    }
    dateCompleted.addOnChange(() => {
        ApplyAnticipatedDelayIsDisabled(dateCompleted, anticipatedDelay);
    })
    anticipatedDelay.addOnChange(() => {
        ApplyDateCompletedIsDisabled(dateCompleted, anticipatedDelay);
    })

    ApplyAnticipatedDelayIsDisabled(dateCompleted, anticipatedDelay);
    ApplyDateCompletedIsDisabled(dateCompleted, anticipatedDelay);

}

function ApplyAnticipatedDelayIsDisabled(dateCompleted: Xrm.Attributes.Attribute, anticipatedDelay: Xrm.Attributes.Attribute): void {
    if (!dateCompleted || !anticipatedDelay) {
        console.warn("ApplyAnticipatedDelayIsDisabled: One or both attributes are undefined or null");
        return;
    }
    const value = dateCompleted.getValue() as Date | null;
    anticipatedDelay.controls.get(0).setDisabled(value !== null);

}

function ApplyDateCompletedIsDisabled(dateCompleted: Xrm.Attributes.Attribute, anticipatedDelay: Xrm.Attributes.Attribute): void {
    if (!dateCompleted || !anticipatedDelay) {
        console.warn("ApplyDateCompletedIsDisabled: One or both attributes are undefined or null");
        return;
    }

    const value = anticipatedDelay.getValue() as number | null;
    dateCompleted.controls.get(0).setDisabled(anticipatedDelay.getIsDirty() && value !== null);
}