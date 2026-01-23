# sua

A Dataverse client-side web resource project built with TypeScript.

## Project Structure

This project follows a structured approach to organizing Dataverse web resource handlers:

```
src/
├── index.ts                 # Main entry point - exports all handlers
└── EntityName/              # Replace with your actual entity name (e.g., Account, Contact)
    ├── index.ts             # Entity-level exports
    ├── OnLoad/              # Form OnLoad event handlers
    │   ├── index.ts         # OnLoad exports
    │   └── HandleSomethingOnLoad.ts
    ├── OnChange/            # Field OnChange event handlers
    │   ├── index.ts         # OnChange exports
    │   └── HandleFieldChange.ts
    └── OnSave/              # Form OnSave event handlers
        ├── index.ts         # OnSave exports
        └── HandleFormSave.ts
```

## Export Patterns

### 1. Individual Function Exports (Recommended)

Export specific functions for each event handler:

```typescript
// src/Account/OnLoad/HandleAccountLoad.ts
export function handleAccountLoad(context: Xrm.Events.EventContext): void {
    const formContext = context.getFormContext();
    // Your logic here
}

// src/Account/OnChange/HandleNameChange.ts
export function handleAccountNameChange(context: Xrm.Events.EventContext): void {
    const formContext = context.getFormContext();
    const nameControl = formContext.getControl('name');
    // Your logic here
}
```

### 2. Namespace/Object Export Pattern

Group related functions under a namespace:

```typescript
// src/Account/AccountHandlers.ts
export const AccountHandlers = {
    onLoad: {
        handleFormLoad: (context: Xrm.Events.EventContext) => {
            // Load logic
        },
        initializeForm: (context: Xrm.Events.EventContext) => {
            // Initialization logic
        }
    },
    onChange: {
        handleNameChange: (context: Xrm.Events.EventContext) => {
            // Name change logic
        },
        handleStatusChange: (context: Xrm.Events.EventContext) => {
            // Status change logic
        }
    }
};
```

### 3. Class-Based Pattern

For complex entities with shared state:

```typescript
// src/Account/AccountManager.ts
export class AccountManager {
    private formContext: Xrm.FormContext;

    constructor(context: Xrm.Events.EventContext) {
        this.formContext = context.getFormContext();
    }

    public handleLoad(): void {
        this.initializeFields();
        this.setupValidation();
    }

    public handleNameChange(): void {
        const name = this.formContext.getAttribute('name')?.getValue();
        // Handle name change
    }

    private initializeFields(): void {
        // Private helper methods
    }
}

// Usage in form events:
export function handleAccountLoad(context: Xrm.Events.EventContext): void {
    const manager = new AccountManager(context);
    manager.handleLoad();
}
```

## Best Practices

### 1. Function Naming

- Use descriptive names that indicate the event and purpose
- Prefix with `handle` for event handlers: `handleAccountLoad`, `handleNameChange`
- Use camelCase for consistency

### 2. Error Handling

Always wrap your handlers in try-catch blocks:

```typescript
export function handleAccountLoad(context: Xrm.Events.EventContext): void {
    try {
        const formContext = context.getFormContext();
        // Your logic here
    } catch (error) {
        console.error('Error in handleAccountLoad:', error);
        // Optional: Show user-friendly message
        Xrm.Navigation.openAlertDialog({
            text: 'An error occurred while loading the form.',
            title: 'Error'
        });
    }
}
```

### 3. Type Safety

Leverage TypeScript for better development experience:

```typescript
interface AccountData {
    name: string;
    accountnumber: string;
    telephone1: string;
}

export function handleAccountLoad(context: Xrm.Events.EventContext): void {
    const formContext = context.getFormContext();

    // Type-safe attribute access
    const nameAttr = formContext.getAttribute<string>('name');
    const phoneAttr = formContext.getAttribute<string>('telephone1');

    if (nameAttr && phoneAttr) {
        // Your logic here
    }
}
```

### 4. Async Operations

Handle asynchronous operations properly:

```typescript
export async function handleAccountLoad(
    context: Xrm.Events.EventContext
): Promise<void> {
    try {
        const formContext = context.getFormContext();

        // Async web API call
        const relatedData = await Xrm.WebApi.retrieveMultipleRecords(
            'contact',
            '?$filter=parentcustomerid eq ' + formContext.data.entity.getId()
        );

        // Process the data
        console.log(`Found ${relatedData.entities.length} related contacts`);
    } catch (error) {
        console.error('Error loading related data:', error);
    }
}
```

### 5. Form Context Validation

Always validate form context availability:

```typescript
export function handleFieldChange(context: Xrm.Events.EventContext): void {
    const formContext = context.getFormContext();

    if (!formContext) {
        console.warn('Form context not available');
        return;
    }

    const attribute = context.getEventSource();
    if (!attribute) {
        console.warn('Event source not available');
        return;
    }

    // Your logic here
}
```

## Dataverse Web API Examples

### Retrieve Records

```typescript
// Get current record data
const recordId = formContext.data.entity.getId();
const entityName = formContext.data.entity.getEntityName();

const record = await Xrm.WebApi.retrieveRecord(
    entityName,
    recordId,
    '?$select=name,telephone1'
);
```

### Create Records

```typescript
const newContact = {
    firstname: 'John',
    lastname: 'Doe',
    emailaddress1: 'john.doe@example.com'
};

const result = await Xrm.WebApi.createRecord('contact', newContact);
```

### Update Records

```typescript
const updateData = {
    telephone1: '555-0123'
};

await Xrm.WebApi.updateRecord(entityName, recordId, updateData);
```

## Building and Deployment

### Development

```bash
# Install dependencies
pnpm install

# Build the project
pnpm run build

# Lint your code
pnpm run lint

# Format your code
pnpm run format
```

### Deployment

1. Run `pnpm run build` to compile TypeScript
2. The compiled JavaScript will be in the `dist/` folder
3. Upload `dist/bundle.js` as a JavaScript web resource in Dataverse
4. Configure form events to call your exported functions

## Form Event Configuration

In Dataverse form designer, reference your functions like this:

- **Library**: `your_webresource_name`
- **Function**: `YourNamespace.handleAccountLoad` (for namespace exports)
- **Function**: `handleAccountLoad` (for direct exports)

## Debugging

Use browser developer tools to debug your web resources:

```typescript
// Add debugging statements
console.log('Form loaded:', formContext.data.entity.getEntityName());
console.debug('Current user:', Xrm.Utility.getGlobalContext().userSettings.userId);

// Set breakpoints in browser dev tools
debugger; // This will pause execution
```

## Additional Resources

- [Microsoft Dataverse Web API Reference](https://docs.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/about)
- [Client API Reference](https://docs.microsoft.com/en-us/power-apps/developer/model-driven-apps/clientapi/reference)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/)

## License

MIT
