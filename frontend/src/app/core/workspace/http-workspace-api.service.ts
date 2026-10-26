import { Injectable, inject } from '@angular/core';
import { forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { WorkspaceApi } from './workspace-api';
import { environment } from '../../../environments/environment';
import {
    AdminSettings,
    AssetDetails,
    AssetClassOption,
    AssetMovementRequest,
    AssetRecord,
    CreateAssetRequest,
    CreateTenantRequest,
    CreatePortalAccountRequest,
    CreateTicketRequest,
    EscalationHistoryEntry,
    EscalationPolicy,
    Escalation,
    FormFieldDefinition,
    OverviewData,
    PortalTicketRequest,
    RepairCloseRequest,
    ReportSummary,
    TicketComment,
    TicketFormDefinition,
    Ticket,
    TicketType,
    TenantRecord,
    CreateUserRequest,
    SystemRoleOption,
    UserRecord,
} from './workspace.models';

@Injectable()
export class HttpWorkspaceApiService extends WorkspaceApi {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = environment.apiBaseUrl;

    getOverview() { return this.http.get<OverviewData>(`${this.baseUrl}/workspace/overview`); }
    getTickets() { return this.http.get<Ticket[]>(`${this.baseUrl}/tickets`); }
    getAssets() { return this.http.get<AssetRecord[]>(`${this.baseUrl}/assets`); }
    getAssetClasses() {
        return this.http.get<{ id: number; code: string; name: string; isActive: boolean }[]>(`${this.baseUrl}/asset-classes`).pipe(
            switchMap(classes => {
                if (!classes.length) return of([] as AssetClassOption[]);
                const requests = classes.map(assetClass => this.http
                    .get<{ code: string; name: string; dataType: string; isRequired: boolean; defaultValue: string | null; options: string | null }[]>(`${this.baseUrl}/asset-classes/${assetClass.id}/attributes`)
                    .pipe(map(attributes => ({
                        id: assetClass.id,
                        code: assetClass.code,
                        name: assetClass.name,
                        active: assetClass.isActive,
                        attributes: attributes.map(attribute => ({
                            code: attribute.code,
                            name: attribute.name,
                            dataType: attribute.dataType,
                            required: attribute.isRequired,
                            defaultValue: attribute.defaultValue ?? undefined,
                            options: parseOptions(attribute.options),
                        })),
                    } satisfies AssetClassOption))));
                return forkJoin(requests);
            }),
        );
    }
    getEscalations() { return this.http.get<Escalation[]>(`${this.baseUrl}/escalations`); }
    createTicket(request: CreateTicketRequest) { return this.http.post<Ticket>(`${this.baseUrl}/tickets`, request); }
    getTicketTypes() { return this.unsupported<TicketType[]>('ticket types'); }
    saveTicketTypes(_ticketTypes: TicketType[]) { return this.unsupported<TicketType[]>('ticket types'); }
    changeTicketStatus(ticketId: string, statusId: number) {
        return this.http.put<Ticket>(`${this.baseUrl}/tickets/${ticketId}/status`, { statusId });
    }
    assignTicket(ticketId: string, assigneeId: number) {
        return this.http.post<Ticket>(`${this.baseUrl}/tickets/${ticketId}/assign`, { assigneeId });
    }
    changeTicketPriority(ticketId: string, priority: string) {
        return this.http.put<Ticket>(`${this.baseUrl}/tickets/${ticketId}`, { priority });
    }
    addTicketComment(ticketId: string, comment: Omit<TicketComment, 'id' | 'createdAt'>) {
        return this.http.post<TicketComment>(`${this.baseUrl}/tickets/${ticketId}/messages`, {
            messageBody: comment.message,
            isInternal: comment.internal,
            attachments: comment.attachments,
        });
    }
    escalateTicket(ticketId: string, initiator: string) { return this.http.post<EscalationHistoryEntry>(`${this.baseUrl}/tickets/${ticketId}/escalations`, { initiator }); }
    getAssetDetails(assetId: string) { return this.http.get<AssetDetails>(`${this.baseUrl}/assets/${assetId}`); }
    createAsset(request: CreateAssetRequest) { return this.http.post<AssetDetails>(`${this.baseUrl}/assets`, request); }
    updateAsset(assetId: string, changes: Partial<CreateAssetRequest>) { return this.http.put<AssetDetails>(`${this.baseUrl}/assets/${assetId}`, changes); }
    deleteAsset(assetId: string) { return this.http.delete<void>(`${this.baseUrl}/assets/${assetId}`); }
    moveAsset(assetId: string, request: AssetMovementRequest) { return this.http.post<AssetDetails>(`${this.baseUrl}/assets/${assetId}/movements`, request); }
    createRepairTicket(assetId: string, title: string) { return this.http.post<Ticket>(`${this.baseUrl}/tickets/repairs`, { assetId, title }); }
    closeRepairTicket(ticketId: string, request: RepairCloseRequest) { return this.http.post<Ticket>(`${this.baseUrl}/tickets/${ticketId}/repair-close`, request); }
    getForms() { return this.unsupported<TicketFormDefinition[]>('ticket forms'); }
    saveForm(_form: TicketFormDefinition) { return this.unsupported<TicketFormDefinition>('ticket forms'); }
    getEscalationPolicies() { return this.unsupported<EscalationPolicy[]>('escalation policies'); }
    saveEscalationPolicies(_policies: EscalationPolicy[]) { return this.unsupported<EscalationPolicy[]>('escalation policies'); }
    getEscalationHistory() { return this.http.get<EscalationHistoryEntry[]>(`${this.baseUrl}/escalations/history`); }
    getAdminSettings() { return this.unsupported<AdminSettings>('admin settings'); }
    saveAdminSettings(_settings: AdminSettings) { return this.unsupported<AdminSettings>('admin settings'); }
    getReports(from?: string, to?: string) { return this.http.get<ReportSummary[]>(`${this.baseUrl}/reports`, { params: { ...(from ? { from } : {}), ...(to ? { to } : {}) } }); }
    getPortalTickets() { return this.http.get<Ticket[]>(`${this.baseUrl}/portal/tickets`); }
    createPortalTicket(request: PortalTicketRequest) {
        const formData = new FormData();
        formData.append('payload', JSON.stringify({ ...request, attachments: undefined }));
        for (const file of request.attachments ?? []) formData.append('attachments', file);
        return this.http.post<Ticket>(`${this.baseUrl}/portal/tickets`, formData);
    }
    addPortalComment(ticketId: string, message: string, attachments: File[]) {
        const formData = new FormData();
        formData.append('payload', JSON.stringify({ messageBody: message, isInternal: false, attachments: attachments.map(file => file.name) }));
        for (const file of attachments) formData.append('attachments', file);
        return this.http.post<TicketComment>(`${this.baseUrl}/tickets/${ticketId}/messages`, formData);
    }
    reviewPortalTicket(ticketId: string, accepted: boolean) { return this.http.post<Ticket>(`${this.baseUrl}/portal/tickets/${ticketId}/review`, { accepted }); }
    createPortalAccount(request: CreatePortalAccountRequest) { return this.http.post<string>(`${this.baseUrl}/portal/accounts`, request); }
    getTenants() { return this.http.get<TenantRecord[]>(`${this.baseUrl}/tenants`); }
    createTenant(request: CreateTenantRequest) { return this.http.post<TenantRecord>(`${this.baseUrl}/tenants`, request); }
    setTenantActive(_tenantId: string, _active: boolean) {
        return throwError(() => new Error('Backend tenant status endpoint is not implemented.'));
    }
    deleteTenant(tenantId: string) { return this.http.delete<void>(`${this.baseUrl}/tenants/${tenantId}`); }
    getUsers() {
        return this.http.get<{ items: UserRecord[] }>(`${this.baseUrl}/users`).pipe(map(result => result.items));
    }
    getSystemRoles() { return this.http.get<SystemRoleOption[]>(`${this.baseUrl}/admin/roles`); }
    createUser(request: CreateUserRequest) { return this.http.post<UserRecord>(`${this.baseUrl}/users`, request); }
    setUserActive(userId: number, isActive: boolean) {
        return this.http.put<UserRecord>(`${this.baseUrl}/users/${userId}/active`, { isActive });
    }

    private unsupported<T>(feature: string) {
        return throwError(() => new Error(`Backend endpoint for ${feature} is not implemented.`));
    }
}

function parseOptions(value: string | null): string[] {
    if (!value) return [];
    try {
        const parsed: unknown = JSON.parse(value);
        if (Array.isArray(parsed)) return parsed.map(String);
    } catch {
        return value.split(',').map(option => option.trim()).filter(Boolean);
    }
    return [];
}