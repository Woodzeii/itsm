export interface WorkspaceMetric {
    label: string;
    value: string;
    change: string;
}

export interface Ticket {
    id: string;
    title: string;
    requester: string;
    team: string;
    priority: string;
    status: string;
    createdAt: string;
    slaRemaining: string;
    type?: string;
    assignee?: string;
    createdOn?: string;
    closedOn?: string;
    reactionSla?: string;
    resolutionSla?: string;
    assetId?: string;
    requesterLogin?: string;
    comments?: TicketComment[];
    history?: TicketHistory[];
    fields?: Record<string, string | number | boolean | null>;
}

export interface TicketComment {
    id: string;
    author: string;
    message: string;
    createdAt: string;
    internal: boolean;
    attachments: string[];
}

export interface TicketHistory {
    id: string;
    event: string;
    actor: string;
    createdAt: string;
}

export interface TicketType {
    id: string;
    name: string;
    portalAvailable: boolean;
    repair: boolean;
    slaEnabled: boolean;
    reactionSla: string;
    resolutionSla: string;
    slaByCriticality?: Record<string, { reaction: string; resolution: string }>;
}

export interface AssetRecord {
    recordId?: string;
    id: string;
    name: string;
    owner: string;
    status: string;
    type: string;
    lifecycle?: AssetLifecycle;
    className?: string;
    warehouse?: string;
    location?: string;
    department?: string;
    attributes?: Record<string, string>;
}

export type AssetLifecycle = 'Закуплен' | 'На складе' | 'В эксплуатации' | 'В ремонте' | 'Списан';

export interface AssetHistoryEntry {
    id: string;
    event: string;
    actor: string;
    createdAt: string;
    details: string;
}

export interface AssetMovement {
    id: string;
    kind: string;
    from: string;
    to: string;
    actor: string;
    createdAt: string;
}

export interface AssetDetails extends AssetRecord {
    history: AssetHistoryEntry[];
    movements: AssetMovement[];
    relatedTickets: Ticket[];
}

export interface CreateAssetRequest {
    id?: string;
    name: string;
    type: string;
    className: string;
    owner?: string;
    warehouse?: string;
    location?: string;
    department?: string;
    attributes?: Record<string, string>;
}

export interface AssetMovementRequest {
    kind: string;
    from: string;
    to: string;
}

export interface ServiceCategory {
    id: string;
    title: string;
    description: string;
    serviceCount: number;
}

export interface Escalation {
    id: string;
    title: string;
    team: string;
    priority: string;
    slaRemaining: string;
}

export interface OverviewData {
    metrics: WorkspaceMetric[];
    series: { label: string; resolved: number; incoming: number }[];
    recentTickets: Ticket[];
    activeEscalations: number;
}

export interface CreateTicketRequest {
    title: string;
    requester: string;
    category?: string;
    type?: string;
    priority?: string;
    description?: string;
    assetId?: string;
    fields?: Record<string, string | number | boolean | null>;
    requesterLogin?: string;
}

export interface FormFieldDefinition {
    id: string;
    name: string;
    type: 'string' | 'textarea' | 'number' | 'date' | 'dictionary' | 'asset' | 'file' | 'boolean';
    required: boolean;
    defaultValue?: string;
    validation?: string;
    hint?: string;
    visibleToRequester: boolean;
    options?: string[];
    conditionFieldId?: string;
    conditionValue?: string;
    immutable?: boolean;
}

export interface TicketFormDefinition {
    ticketTypeId: string;
    ticketTypeName: string;
    fields: FormFieldDefinition[];
}

export interface EscalationPolicy {
    ticketTypeId: string;
    ticketTypeName: string;
    onResolutionBreach: boolean;
    allowManualByRequester: boolean;
    allowManualByAgent: boolean;
    manager: string;
}

export interface EscalationHistoryEntry {
    ticketId: string;
    initiator: string;
    createdAt: string;
    previousPriority: string;
    newPriority: string;
    recipients: string[];
}

export interface AdminSettings {
    assignmentMode: 'automatic' | 'manual';
    manager: string;
    slaEnabled: boolean;
    slaMode: '24/7' | 'working-hours';
    workingHours: string;
    criticalities: string[];
    statuses: { name: string; pausesSla: boolean; immutable: boolean }[];
    portalSlaFields: string[];
    portalAccounts?: string[];
    engineers?: string[];
    tenantMode?: 'on-premise' | 'saas';
    passwordPolicy?: { minLength: number; requireUppercase: boolean; requireSpecial: boolean; expiryDays: number; maxFailedAttempts: number; lockoutMinutes: number };
    reportAccessRoles?: string[];
}

export interface ReportSummary {
    id: string;
    title: string;
    description: string;
    value: string;
}

export interface PortalTicketRequest extends CreateTicketRequest {
    priority: string;
    type: string;
    description: string;
    assetId?: string;
    attachments?: File[];
}

export interface RepairCloseRequest {
    result: 'В эксплуатацию' | 'На склад' | 'Списать';
    workDescription: string;
}

export interface CreatePortalAccountRequest {
    username: string;
    temporaryPassword: string;
}

export interface TenantRecord {
    id: string;
    name: string;
    firstAdminLogin: string;
    active: boolean;
}

export interface CreateTenantRequest {
    name: string;
    firstAdminLogin: string;
}