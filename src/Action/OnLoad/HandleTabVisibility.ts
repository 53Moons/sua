import { CheckForAttributeValue, ToogleTabVisibility } from '../../General';

export interface FormProperty {
    attributeName: string;
    tabName: string;
}

export async function HandleTabVisibility(context: Xrm.Events.EventContext) {
    if (!context) {
        console.error('ToggleTabVisibility: context is undefined or null');
        return;
    }

    var formContext = context.getFormContext();

    var formProperties: FormProperty[] = [
        { attributeName: 'sua_environmental', tabName: 'Environmental' },
        { attributeName: 'sua_aeronautical', tabName: 'Aeronautical' }
    ];

    for (var formProperty of formProperties) {
        var hasValue = CheckForAttributeValue(formContext, formProperty.attributeName);
        console.debug(
            `Checking attribute: ${formProperty.attributeName}, hasValue: ${hasValue}`
        );
        ToogleTabVisibility(formContext, formProperty.tabName, hasValue);
    }
}
