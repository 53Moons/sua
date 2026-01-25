
export function ControlAeronauticalMilestoneSubgridAttributeEdit(context: Xrm.Events.EventContext) {
    if (!context) {
        console.error("ControlAeronauticalMilestoneSubgridAttributeEdit: context is undefined or null");
        return;
    }
    const formContext = context.getFormContext();
    const eligibleAttributes = ["sua_datecompleted", "sua_anticipateddelay"];
    const entity = formContext.data.entity;
    entity.attributes.forEach(attr => {
        const name = attr.getName();
        if (!name) {
            console.warn("ControlAeronauticalMilestoneSubgridAttributeEdit: Attribute name is undefined or null");
            return;
        }
        const control = attr.controls.get(0);
        const shouldHide = !eligibleAttributes.includes(name);
        control.setDisabled(shouldHide);

    });

}