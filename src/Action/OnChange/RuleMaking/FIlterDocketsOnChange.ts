import { v4 as uuidv4 } from 'uuid';

export function FilterDocketsOnChange(context: Xrm.Events.EventContext) {
    if (!context) {
        console.error('FilterDocketsOnChange: context is undefined or null');
        return;
    }

    var formContext = context.getFormContext();

    var ruleMakingToggle =
        formContext.getAttribute<Xrm.Attributes.BooleanAttribute>('sua_rulemaking');
    if (!ruleMakingToggle) {
        console.error(
            'FilterDocketsOnChange: sua_rulemaking attribute not found on form'
        );
        return;
    }

    var isRuleMaking = ruleMakingToggle.getValue();

    const docketAttribute =
        formContext.getAttribute<Xrm.Attributes.LookupAttribute>('sua_docket');
    if (!docketAttribute) {
        console.error('FilterDocketsOnChange: sua_docket attribute not found on form');
        return;
    }

    const entityName = isRuleMaking ? 'w21_rulemakingdocket' : 'w21_nrdocket';
    const fetchXml = `
    <fetch>
        <entity name="${entityName}"> 
        </entity>
    </fetch>
    `;
    const layoutXml = `
    <grid name="AvailableDockets" object="1" jump="w21_name"
                select="w21_name" icon="1" preview="1">
        <row name="AvailableDocketsRow" id="${entityName}id">
            <cell name="w21_name" width="150" />
        </row>
    </grid>
    `;

    docketAttribute.controls.forEach((control) => {
        control.addCustomView(
            uuidv4(),
            entityName,
            'Available Dockets',
            fetchXml,
            layoutXml,
            true
        );
    });
}
