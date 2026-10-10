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
}

export interface AssetRecord {
    id: string;
    name: string;
    owner: string;
    status: string;
    type: string;
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
}