import {v4 as uuidv4 } from 'uuid'

interface CascadedEntity {
    entityName: string,
    lookupAttributeName: string,
    attributeName: string,
    viewName: string,
    layoutXML: string
}

export function HandleDistrictCascade(context: Xrm.Events.EventContext) {
    if (!context) {
        console.error("HandleDistrictCascade: context is undefined or null");
        return;
    }

    var formContext = context.getFormContext();

    const districtAttr = formContext.getAttribute<Xrm.Attributes.LookupAttribute>("sua_district");
    const district = districtAttr?.getValue()?.[0] ?? null;
    if (!district) {
        console.debug("HandleDistrictCascade: district is undefined or null");
        return;
    }
    console.debug(`HandleDistrictCascade: district is ${district.name} (${district.id})`);

    const entities: CascadedEntity[] = [
        {
            entityName: "loc_facility",
            lookupAttributeName: "loc_district",
            attributeName: "sua_primaryfacility",
            viewName: "Available Facilities",
            layoutXML: `
            <grid name="AvailableFacilities" object="1" jump="loc_name"
                select="loc_name" icon="1" preview="1">
                <row name="AvailableFacilitiesRow" id="loc_facilityid">
                    <cell name="loc_name" width="150" />
                    <cell name="loc_fullname" width="150" />
                </row>
            </grid>
            `
        },
        {
            entityName: "sua_airtrafficrepresentative",
            lookupAttributeName: "sua_district",
            attributeName: "sua_airtrafficrepresentative",
            viewName: "Available ATREP",
            layoutXML: `
            <grid name="AvailableATREP" object="1" jump="sua_name"
                select="sua_name" icon="1" preview="1">
                <row name="AvailableATREPRow" id="sua_airtrafficrepresentativeid">
                    <cell name="sua_name" width="150" />
                </row>
            </grid>
            `
        }
    ];

    entities.forEach((entity) => {
        var fetchXML = GenerateFetchXML(entity, district.id);
        if (!fetchXML || fetchXML.trim() === "") {
            console.error("HandleDistrictCascade: fetchXML is empty or invalid");
            return;
        }
        const attribute = formContext.getAttribute(entity.attributeName) as Xrm.Attributes.LookupAttribute;
        if (!attribute) {
            console.error(`HandleDistrictCascade: attribute ${entity.attributeName} not found on form`);
            return;
        }
        const viewId = uuidv4();
        attribute.controls.forEach(control => {
            control.addCustomView(viewId, entity.entityName, entity.viewName, fetchXML, entity.layoutXML, true);
        });
        console.debug(`HandleDistrictCascade: added custom view for ${entity.attributeName} (viewId=${viewId})`);
    });
}

function GenerateFetchXML(entity: CascadedEntity, districtId: string): string {
    var fetchXML = "";
    if (!entity) {
        console.error("GenerateFetchXML: entity is undefined or null");
        return fetchXML;
    }
    if (!districtId) {
        console.error("GenerateFetchXML: districtId is undefined or null");
        return fetchXML;
    }

    fetchXML = `
    <fetch>
        <entity name="${entity.entityName}">
            <filter>
                <condition attribute="${entity.lookupAttributeName}" operator="eq" value="${districtId}" />
            </filter>
        </entity>
    </fetch>
    `;

    return fetchXML;
}