# Workflow Form Schema Design

**Goal:** Replace manual workflow start JSON entry with configurable workflow form fields while preserving `FormDataJson` for condition branches and existing integrations.

**Scope**
- Add a workflow definition form schema stored as JSON.
- Support v1 controls: text, textarea, number, date, select.
- Each field supports label, field code, control type, required flag, default value, placeholder, and select options.
- The workflow definition editor configures fields in a simple table.
- The workflow start panel renders fields dynamically from the selected definition.
- Submitted values are serialized to existing `FormDataJson`.
- Workflow details display form data as a readable field list and keep a JSON preview.

**Out Of Scope**
- Drag-and-drop form designer.
- Multi-column layouts.
- Field visibility/linkage rules.
- Regex validation and complex expressions.
- File upload controls.
- Business-module-specific forms.

**Data Model**
- Add `WorkflowDefinition.FormSchemaJson` as `longtext`, defaulting to `[]`.
- Add `FormSchemaJson` to workflow definition DTOs and save requests.
- Existing workflows with no schema behave as before and still show the JSON editor.

**Backend Validation**
- Definition save validates form schema JSON shape.
- Allowed component types: `text`, `textarea`, `number`, `date`, `select`.
- Field codes must be unique and use simple identifiers: letters, numbers, and underscore, not starting with a number.
- Start workflow validates required fields in `FormDataJson` when the definition has a schema.
- Number fields must be numeric when provided.
- Select fields must use one of the configured option values when options exist.

**Frontend Behavior**
- Definition form adds a "表单字段" section.
- Start form uses schema fields when present; otherwise keeps the JSON textarea fallback.
- Changing workflow definition resets the dynamic form from schema defaults.
- Submit serializes dynamic form values into JSON before calling the existing start API.

**Testing**
- Backend tests cover schema persistence, invalid schema rejection, required field rejection, and valid dynamic form start.
- Frontend build verifies type and template correctness.
